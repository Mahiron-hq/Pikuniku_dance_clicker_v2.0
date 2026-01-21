using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using OpenCvSharp;
using Serilog;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace Pikuniku_dance_clicker_v2
{
    public class ScreenCapture : IDisposable
    {
        private Device _device;
        private OutputDuplication _outputDuplication;
        private Texture2D _stagingTexture;
        private int _screenWidth;
        private int _screenHeight;

        public int ScreenWidth => _screenWidth;
        public int ScreenHeight => _screenHeight;

        // Статистика
        private long _totalFramesCaptured = 0;
        private long _missedFrames = 0;
        private readonly Stopwatch _captureTimer = new Stopwatch();

        public ScreenCapture()
        {
            InitializeCapture();
        }

        private void InitializeCapture()
        {
            var sw = Stopwatch.StartNew();
            
            using (var factory = new Factory1())
            {
                using (var adapter = factory.GetAdapter1(0))
                {
                    _device = new Device(adapter);
                    Log.Verbose($"DirectX Device создан: {adapter.Description.Description}");

                    using (var output = adapter.GetOutput(0))
                    {
                        using (var output1 = output.QueryInterface<Output1>())
                        {
                            _screenWidth = output.Description.DesktopBounds.Right - output.Description.DesktopBounds.Left;
                            _screenHeight = output.Description.DesktopBounds.Bottom - output.Description.DesktopBounds.Top;

                            _outputDuplication = output1.DuplicateOutput(_device);

                            var textureDesc = new Texture2DDescription
                            {
                                CpuAccessFlags = CpuAccessFlags.Read,
                                BindFlags = BindFlags.None,
                                Format = Format.B8G8R8A8_UNorm,
                                Width = _screenWidth,
                                Height = _screenHeight,
                                OptionFlags = ResourceOptionFlags.None,
                                MipLevels = 1,
                                ArraySize = 1,
                                SampleDescription = { Count = 1, Quality = 0 },
                                Usage = ResourceUsage.Staging
                            };

                            _stagingTexture = new Texture2D(_device, textureDesc);
                            
                            sw.Stop();
                            Log.Information($"Захват экрана инициализирован: {_screenWidth}x{_screenHeight} за {sw.ElapsedMilliseconds}ms");
                            Log.Verbose($"Формат текстуры: {textureDesc.Format}, Размер буфера: {(_screenWidth * _screenHeight * 4) / 1024}KB");
                        }
                    }
                }
            }
        }

        public Mat CaptureFull()
        {
            return CaptureRegion(0, 0, _screenWidth, _screenHeight);
        }

        public Mat CaptureBottomHalf()
        {
            int startY = _screenHeight / 2;
            int height = _screenHeight / 2;
            return CaptureRegion(0, startY, _screenWidth, height);
        }

        public Mat CaptureRegionDirect(int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x + width > _screenWidth || y + height > _screenHeight)
            {
                Log.Warning($"ROI выходит за границы: ({x},{y}) {width}x{height} при экране {_screenWidth}x{_screenHeight}");
                return null;
            }

            _captureTimer.Restart();

            try
            {
                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation frameInfo;
                
                var acquireResult = _outputDuplication.TryAcquireNextFrame(16, out frameInfo, out screenResource);
                
                if (acquireResult.Failure)
                {
                    if (acquireResult.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Code)
                    {
                        _missedFrames++;
                        Log.Verbose($"Пропущен кадр (WaitTimeout), всего пропущено: {_missedFrames}");
                    }
                    else
                    {
                        Log.Warning($"Ошибка захвата кадра: {acquireResult.Code:X}");
                    }
                    return null;
                }

                var copyStart = _captureTimer.ElapsedMilliseconds;

                using (var screenTexture = screenResource.QueryInterface<Texture2D>())
                {
                    var region = new ResourceRegion(x, y, 0, x + width, y + height, 1);
                    _device.ImmediateContext.CopySubresourceRegion(
                        screenTexture, 0, region,
                        _stagingTexture, 0, 0, 0, 0
                    );
                }

                screenResource?.Dispose();
                _outputDuplication.ReleaseFrame();

                var copyTime = _captureTimer.ElapsedMilliseconds - copyStart;
                var mapStart = _captureTimer.ElapsedMilliseconds;

                var dataBox = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, MapFlags.None);

                try
                {
                    Mat resultMat = new Mat(height, width, MatType.CV_8UC4);
                    
                    unsafe
                    {
                        byte* srcPtr = (byte*)dataBox.DataPointer.ToPointer();
                        byte* dstPtr = (byte*)resultMat.Data.ToPointer();
                        
                        for (int row = 0; row < height; row++)
                        {
                            System.Buffer.MemoryCopy(
                                srcPtr + row * dataBox.RowPitch,
                                dstPtr + row * width * 4,
                                width * 4,
                                width * 4
                            );
                        }
                    }

                    _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);

                    var mapTime = _captureTimer.ElapsedMilliseconds - mapStart;
                    
                    _totalFramesCaptured++;

                    _captureTimer.Stop();
                    var totalTime = _captureTimer.ElapsedMilliseconds;

                    Log.Verbose($"Захват ROI {width}x{height}: Acquire={copyStart}ms, Copy={copyTime}ms, Map={mapTime}ms, Total={totalTime}ms");

                    // Возвращаем BGRA напрямую без конвертации
                    return resultMat;
                }
                catch (Exception ex)
                {
                    _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
                    Log.Error(ex, "Ошибка при копировании данных текстуры");
                    throw;
                }
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode == SharpDX.DXGI.ResultCode.WaitTimeout)
                {
                    _missedFrames++;
                    Log.Verbose($"Timeout при захвате кадра");
                    return null;
                }
                
                if (ex.ResultCode == SharpDX.DXGI.ResultCode.AccessLost)
                {
                    Log.Warning("Доступ к дисплею потерян (смена разрешения?), переинициализация...");
                    Reinitialize();
                    return null;
                }

                Log.Error($"Ошибка DirectX: {ex.ResultCode.ToString()} (Code: {ex.ResultCode.Code})");
                return null;
            }
        }

        private Mat CaptureRegion(int x, int y, int width, int height)
        {
            _captureTimer.Restart();

            try
            {
                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation frameInfo;
                
                var result = _outputDuplication.TryAcquireNextFrame(16, out frameInfo, out screenResource);
                
                if (result.Failure)
                {
                    _missedFrames++;
                    return null;
                }

                using (var screenTexture = screenResource.QueryInterface<Texture2D>())
                {
                    _device.ImmediateContext.CopyResource(screenTexture, _stagingTexture);
                }

                screenResource?.Dispose();
                _outputDuplication.ReleaseFrame();

                var dataBox = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, MapFlags.None);

                try
                {
                    Mat fullMat = new Mat(_screenHeight, _screenWidth, MatType.CV_8UC4);
                    
                    unsafe
                    {
                        byte* srcPtr = (byte*)dataBox.DataPointer.ToPointer();
                        byte* dstPtr = (byte*)fullMat.Data.ToPointer();
                        
                        for (int row = 0; row < _screenHeight; row++)
                        {
                            System.Buffer.MemoryCopy(
                                srcPtr + row * dataBox.RowPitch,
                                dstPtr + row * _screenWidth * 4,
                                _screenWidth * 4,
                                _screenWidth * 4
                            );
                        }
                    }

                    _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);

                    using (fullMat)
                    {
                        Mat regionMat = new Mat(fullMat, new Rect(x, y, width, height));
                        Mat resultMat = regionMat.Clone();
                        regionMat.Dispose();
                        
                        _totalFramesCaptured++;
                        _captureTimer.Stop();
                        
                        return resultMat;
                    }
                }
                catch
                {
                    _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
                    throw;
                }
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode == SharpDX.DXGI.ResultCode.AccessLost)
                {
                    Log.Warning("Доступ к дисплею потерян, переинициализация...");
                    Reinitialize();
                }
                return null;
            }
        }

        public void LogStatistics()
        {
            double missRate = _totalFramesCaptured > 0 
                ? (_missedFrames * 100.0 / (_totalFramesCaptured + _missedFrames)) 
                : 0;
            
            Log.Information($"Статистика захвата: Захвачено={_totalFramesCaptured}, Пропущено={_missedFrames} ({missRate:F2}%)");
        }

        private void Reinitialize()
        {
            Dispose();
            InitializeCapture();
        }

        public void Dispose()
        {
            LogStatistics();
            _outputDuplication?.Dispose();
            _stagingTexture?.Dispose();
            _device?.Dispose();
        }
    }
}