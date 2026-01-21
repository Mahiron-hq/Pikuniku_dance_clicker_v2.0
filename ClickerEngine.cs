using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Linq;
using OpenCvSharp;
using Serilog;
using InputSimulatorStandard;
using InputSimulatorStandard.Native;
using Microsoft.Extensions.Configuration;

namespace Pikuniku_dance_clicker_v2
{
    public class ClickerEngine : IDisposable
    {
        private readonly Action<string, bool, bool> _updateStatus;
        private readonly IConfiguration _config;
        private Thread _workerThread;
        private CancellationTokenSource _cts;
        private ScreenCapture _capture;
        private InputSimulator _inputSim;

        private Point _centrePoint;
        private double _scaleFactor = 1.0;
        private Rect _searchRoi;

        // Оригинальные шаблоны
        private Mat _templateCentreOriginal;
        private Mat _templateUpOriginal;
        private Mat _templateDownOriginal;
        private Mat _templateLeftOriginal;
        private Mat _templateRightOriginal;

        // Масштабированные шаблоны в BGRA
        private Mat _templateCentre;
        private Mat _templateUp;
        private Mat _templateDown;
        private Mat _templateLeft;
        private Mat _templateRight;

        private readonly Stopwatch _sessionTimer = new Stopwatch();
        private readonly Stopwatch _lastPressTimer = new Stopwatch();
        private const int MIN_PRESS_INTERVAL_MS = 100;

        // ФИКСИРОВАННЫЙ порог (как было в рабочей версии)
        private const double CALIBRATION_THRESHOLD = 0.8;
        private const double DETECTION_THRESHOLD = 0.75;

        // Статистика
        private readonly ConcurrentDictionary<string, int> _detectionStats = new();
        private readonly ConcurrentDictionary<string, int> _rejectionStats = new();
        private int _totalFramesProcessed = 0;
        private int _totalDetections = 0;
        private readonly Stopwatch _statsTimer = new Stopwatch();

        public ClickerEngine(Action<string, bool, bool> updateStatus, IConfiguration config)
        {
            _updateStatus = updateStatus;
            _config = config;
            _inputSim = new InputSimulator();
            
            _detectionStats["UP"] = 0;
            _detectionStats["DOWN"] = 0;
            _detectionStats["LEFT"] = 0;
            _detectionStats["RIGHT"] = 0;
        }

        public void Start()
        {
            if (_workerThread != null && _workerThread.IsAlive)
            {
                Log.Warning("Попытка запуска уже работающего движка");
                return;
            }

            _cts = new CancellationTokenSource();
            _workerThread = new Thread(() => WorkerLoop(_cts.Token))
            {
                Priority = ThreadPriority.Highest,
                IsBackground = true,
                Name = "ClickerWorkerThread"
            };
            _workerThread.Start();
            Log.Information("Рабочий поток запущен с приоритетом Highest");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _workerThread?.Join(2000);
            LogFinalStatistics();
            Log.Information("Рабочий поток остановлен");
        }

        private void WorkerLoop(CancellationToken token)
        {
            _sessionTimer.Start();
            
            try
            {
                _updateStatus(Localization.Get("StatusInitCapture"), false, false);
                _capture = new ScreenCapture();
                Log.Information(Localization.Get("LogCaptureInit", _capture.ScreenWidth, _capture.ScreenHeight));

                LoadTemplates();
                _updateStatus(Localization.Get("StatusTemplatesLoaded"), false, false);

                if (!FindCentre(token))
                {
                    _updateStatus(Localization.Get("ErrorCenterNotFound"), true, false);
                    return;
                }

                if (!CalibrateScaleWithRightArrow(token))
                {
                    _updateStatus(Localization.Get("ErrorCalibrationFailed"), true, false);
                    return;
                }

                ApplyScaleToTemplatesBGRA();
                CalculateOptimalROI();
                
                _updateStatus(Localization.Get("StatusWorking", _searchRoi.Width, _searchRoi.Height), false, true);
                _lastPressTimer.Start();
                _statsTimer.Start();

                MainLoop(token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Критическая ошибка в рабочем потоке");
                _updateStatus(Localization.Get("ErrorCritical", ex.Message), true, false);
            }
            finally
            {
                CleanupResources();
            }
        }

        private void LoadTemplates()
        {
            var sw = Stopwatch.StartNew();
            
            _templateCentreOriginal = Cv2.ImRead("Assets/centre.png", ImreadModes.Color);
            _templateUpOriginal = Cv2.ImRead("Assets/up.png", ImreadModes.Color);
            _templateDownOriginal = Cv2.ImRead("Assets/down.png", ImreadModes.Color);
            _templateLeftOriginal = Cv2.ImRead("Assets/left.png", ImreadModes.Color);
            _templateRightOriginal = Cv2.ImRead("Assets/right.png", ImreadModes.Color);

            if (_templateCentreOriginal.Empty() || _templateUpOriginal.Empty() || 
                _templateDownOriginal.Empty() || _templateLeftOriginal.Empty() || 
                _templateRightOriginal.Empty())
            {
                throw new Exception("Не удалось загрузить шаблоны из Assets");
            }

            sw.Stop();
            Log.Information(Localization.Get("LogTemplatesLoaded", sw.ElapsedMilliseconds));
        }

        private bool FindCentre(CancellationToken token)
        {
            _updateStatus(Localization.Get("StatusFindingCenter"), false, false);
            var timeout = Stopwatch.StartNew();
            int attempts = 0;

            while (timeout.ElapsedMilliseconds < 10000 && !token.IsCancellationRequested)
            {
                attempts++;
                using (var frame = _capture.CaptureBottomHalf())
                {
                    if (frame == null || frame.Empty()) continue;

                    using (var frameBGR = new Mat())
                    using (var result = new Mat())
                    {
                        Cv2.CvtColor(frame, frameBGR, ColorConversionCodes.BGRA2BGR);
                        Cv2.MatchTemplate(frameBGR, _templateCentreOriginal, result, TemplateMatchModes.CCoeffNormed);
                        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

                        Log.Verbose($"Поиск центра попытка #{attempts}: точность={maxVal:F3}");

                        if (maxVal >= CALIBRATION_THRESHOLD)
                        {
                            int offsetY = _capture.ScreenHeight / 2;
                            _centrePoint = new Point(
                                maxLoc.X + _templateCentreOriginal.Width / 2,
                                maxLoc.Y + _templateCentreOriginal.Height / 2 + offsetY
                            );
                            
                            Log.Information(Localization.Get("LogCenterFound", timeout.ElapsedMilliseconds, 
                                                           _centrePoint.X, _centrePoint.Y, maxVal));
                            return true;
                        }
                    }
                }
                Thread.Sleep(16);
            }

            Log.Error($"Таймаут поиска центра после {attempts} попыток");
            return false;
        }

        private bool CalibrateScaleWithRightArrow(CancellationToken token)
        {
            _updateStatus(Localization.Get("StatusCalibrating"), false, false);
            var timeout = Stopwatch.StartNew();

            int searchX = _centrePoint.X;
            int searchY = _centrePoint.Y - _templateCentreOriginal.Height / 2;
            int searchWidth = _capture.ScreenWidth - _centrePoint.X;
            int searchHeight = _templateCentreOriginal.Height;

            if (searchY < 0) searchY = 0;
            if (searchY + searchHeight > _capture.ScreenHeight) 
                searchHeight = _capture.ScreenHeight - searchY;

            Log.Information($"Область калибровки: X={searchX}, Y={searchY}, W={searchWidth}, H={searchHeight}");

            int attempts = 0;
            while (timeout.ElapsedMilliseconds < 10000 && !token.IsCancellationRequested)
            {
                attempts++;
                using (var frame = _capture.CaptureRegionDirect(searchX, searchY, searchWidth, searchHeight))
                {
                    if (frame == null || frame.Empty()) continue;

                    using (var frameBGR = new Mat())
                    {
                        Cv2.CvtColor(frame, frameBGR, ColorConversionCodes.BGRA2BGR);

                        for (double scale = 0.6; scale <= 1.4; scale += 0.02)
                        {
                            if (token.IsCancellationRequested) break;

                            int newWidth = (int)(_templateRightOriginal.Width * scale);
                            int newHeight = (int)(_templateRightOriginal.Height * scale);

                            if (newWidth < 5 || newHeight < 5 || newWidth > frameBGR.Width || newHeight > frameBGR.Height)
                                continue;

                            using (var scaledTemplate = new Mat())
                            using (var result = new Mat())
                            {
                                Cv2.Resize(_templateRightOriginal, scaledTemplate, new Size(newWidth, newHeight), 
                                          0, 0, InterpolationFlags.Linear);
                                Cv2.MatchTemplate(frameBGR, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);

                                if (maxVal >= CALIBRATION_THRESHOLD)
                                {
                                    _scaleFactor = scale;
                                    Log.Information($"✓ Масштаб найден за {timeout.ElapsedMilliseconds}ms: {_scaleFactor:F2} (точность={maxVal:F3}, попыток={attempts})");
                                    return true;
                                }
                            }
                        }
                    }
                }
                Thread.Sleep(16);
            }

            Log.Warning($"Стрелка не найдена за {attempts} попыток, масштаб=1.0");
            _scaleFactor = 1.0;
            return true;
        }

        private void ApplyScaleToTemplatesBGRA()
        {
            _updateStatus(Localization.Get("StatusCalibrating") + $" {_scaleFactor:F2}...", false, false);
            var sw = Stopwatch.StartNew();

            _templateCentre = ResizeAndConvertToBGRA(_templateCentreOriginal, _scaleFactor);
            _templateUp = ResizeAndConvertToBGRA(_templateUpOriginal, _scaleFactor);
            _templateDown = ResizeAndConvertToBGRA(_templateDownOriginal, _scaleFactor);
            _templateLeft = ResizeAndConvertToBGRA(_templateLeftOriginal, _scaleFactor);
            _templateRight = ResizeAndConvertToBGRA(_templateRightOriginal, _scaleFactor);

            sw.Stop();
            Log.Information($"Шаблоны масштабированы в BGRA за {sw.ElapsedMilliseconds}ms: " +
                          $"centre={_templateCentre.Width}x{_templateCentre.Height}, " +
                          $"arrows={_templateUp.Width}x{_templateUp.Height}");
        }

        private Mat ResizeAndConvertToBGRA(Mat original, double scale)
        {
            Mat resized;
            
            if (Math.Abs(scale - 1.0) < 0.01)
            {
                resized = original.Clone();
            }
            else
            {
                int newWidth = (int)(original.Width * scale);
                int newHeight = (int)(original.Height * scale);
                resized = new Mat();
                Cv2.Resize(original, resized, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Linear);
            }

            Mat bgra = new Mat();
            Cv2.CvtColor(resized, bgra, ColorConversionCodes.BGR2BGRA);
            resized.Dispose();
            
            return bgra;
        }

        private void CalculateOptimalROI()
        {
            // ВЕРНУЛИ КАК БЫЛО В РАБОЧЕЙ ВЕРСИИ
            int padding = (int)(15 * _scaleFactor);
            int rightExtension = 15;
            int roiX = _centrePoint.X - _templateCentre.Width / 2 - padding;
            int roiY = _centrePoint.Y - _templateCentre.Height / 2 - padding;
            int roiWidth = _templateCentre.Width + padding * 2 + rightExtension;
            int roiHeight = _templateCentre.Height + padding * 2;

            roiX = Math.Max(0, Math.Min(roiX, _capture.ScreenWidth - roiWidth));
            roiY = Math.Max(0, Math.Min(roiY, _capture.ScreenHeight - roiHeight));

            _searchRoi = new Rect(roiX, roiY, roiWidth, roiHeight);
            
            double roiPercent = (_searchRoi.Width * _searchRoi.Height * 100.0) / (_capture.ScreenWidth * _capture.ScreenHeight);
            Log.Information($"ROI установлена: {_searchRoi.X},{_searchRoi.Y} {_searchRoi.Width}x{_searchRoi.Height} ({roiPercent:F2}% экрана)");
        }

        private void MainLoop(CancellationToken token)
        {
            int frameCount = 0;
            var fpsTimer = Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var roiFrame = _capture.CaptureRegionDirect(
                        _searchRoi.X, _searchRoi.Y, _searchRoi.Width, _searchRoi.Height))
                    {
                        if (roiFrame != null && !roiFrame.Empty())
                        {
                            ProcessFrameParallel(roiFrame);
                            _totalFramesProcessed++;
                        }
                    }

                    frameCount++;
                    if (fpsTimer.ElapsedMilliseconds >= 1000)
                    {
                        double detectRate = frameCount > 0 ? (_totalDetections * 100.0 / frameCount) : 0;
                        Log.Debug($"[{_sessionTimer.Elapsed:mm\\:ss}] FPS: {frameCount}, Обнаружений: {_totalDetections} ({detectRate:F1}%)");
                        
                        frameCount = 0;
                        _totalDetections = 0;
                        fpsTimer.Restart();
                    }

                    if (_statsTimer.ElapsedMilliseconds >= 5000)
                    {
                        LogDetectionStatistics();
                        _statsTimer.Restart();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка в главном цикле");
                }
            }
        }

        private void ProcessFrameParallel(Mat roiFrame)
        {
            var frameTimer = Stopwatch.StartNew();
            
            if (_lastPressTimer.ElapsedMilliseconds < MIN_PRESS_INTERVAL_MS)
            {
                return;
            }

            // ПАРАЛЛЕЛЬНЫЙ ПОИСК
            var results = new ConcurrentBag<(string name, VirtualKeyCode key, double confidence, Point location)>();

            Parallel.Invoke(
                () => CheckTemplateParallel(roiFrame, _templateUp, VirtualKeyCode.UP, "UP", results),
                () => CheckTemplateParallel(roiFrame, _templateDown, VirtualKeyCode.DOWN, "DOWN", results),
                () => CheckTemplateParallel(roiFrame, _templateLeft, VirtualKeyCode.LEFT, "LEFT", results),
                () => CheckTemplateParallel(roiFrame, _templateRight, VirtualKeyCode.RIGHT, "RIGHT", results)
            );

            if (results.Count == 0) return;

            // Выбираем лучший результат
            var bestResult = results.OrderByDescending(r => r.confidence).First();

            // НАЖИМАЕМ СРАЗУ БЕЗ ПРОВЕРКИ ЦЕНТРИРОВАНИЯ
            _inputSim.Keyboard.KeyPress(bestResult.key);
            
            _detectionStats[bestResult.name]++;
            _totalDetections++;
            _lastPressTimer.Restart();
            
            frameTimer.Stop();
            Log.Information($"[{_sessionTimer.Elapsed:mm\\:ss\\.fff}] ✓ {bestResult.name} (conf={bestResult.confidence:F3}, pos={bestResult.location.X},{bestResult.location.Y}, time={frameTimer.ElapsedMilliseconds}ms)");
        }

        private void CheckTemplateParallel(Mat scene, Mat template, VirtualKeyCode key, string name,
            ConcurrentBag<(string, VirtualKeyCode, double, Point)> results)
        {
            using (var result = new Mat())
            {
                Cv2.MatchTemplate(scene, template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

                // УБРАЛИ ПРОВЕРКУ ЦЕНТРИРОВАНИЯ - нажимаем сразу при обнаружении
                if (maxVal >= DETECTION_THRESHOLD)
                {
                    results.Add((name, key, maxVal, maxLoc));
                    Log.Verbose($"{name}: conf={maxVal:F3}, pos=({maxLoc.X},{maxLoc.Y})");
                }
                else if (maxVal >= 0.60)
                {
                    _rejectionStats.AddOrUpdate(name + "_threshold", 1, (k, v) => v + 1);
                    Log.Verbose($"{name} отклонена (порог): conf={maxVal:F3} < {DETECTION_THRESHOLD:F2}");
                }
            }
        }

        private void LogDetectionStatistics()
        {
            Log.Information($"=== Статистика за последние 5 секунд ===");
            Log.Information($"Обнаружения: UP={_detectionStats["UP"]}, DOWN={_detectionStats["DOWN"]}, " +
                          $"LEFT={_detectionStats["LEFT"]}, RIGHT={_detectionStats["RIGHT"]}");
            
            if (_rejectionStats.Count > 0)
            {
                Log.Debug("Отклонения:");
                foreach (var kvp in _rejectionStats)
                {
                    Log.Debug($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }

        private void LogFinalStatistics()
        {
            _sessionTimer.Stop();
            Log.Information($"=== Финальная статистика сессии ({_sessionTimer.Elapsed:mm\\:ss}) ===");
            Log.Information($"Кадров обработано: {_totalFramesProcessed}");
            
            int totalDetections = 0;
            foreach (var kvp in _detectionStats)
            {
                totalDetections += kvp.Value;
                Log.Information($"  {kvp.Key}: {kvp.Value} нажатий");
            }
            
            double avgFPS = _sessionTimer.Elapsed.TotalSeconds > 0 
                ? _totalFramesProcessed / _sessionTimer.Elapsed.TotalSeconds 
                : 0;
            
            Log.Information($"Средний FPS: {avgFPS:F1}");
            Log.Information($"Всего нажатий: {totalDetections}");
        }

        private void CleanupResources()
        {
            _templateCentreOriginal?.Dispose();
            _templateUpOriginal?.Dispose();
            _templateDownOriginal?.Dispose();
            _templateLeftOriginal?.Dispose();
            _templateRightOriginal?.Dispose();

            _templateCentre?.Dispose();
            _templateUp?.Dispose();
            _templateDown?.Dispose();
            _templateLeft?.Dispose();
            _templateRight?.Dispose();

            _capture?.Dispose();
            Log.Information("Ресурсы очищены");
        }

        public void Dispose()
        {
            Stop();
            CleanupResources();
        }
    }
}