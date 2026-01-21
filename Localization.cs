using System;
using System.Collections.Generic;
using System.Globalization;

namespace Pikuniku_dance_clicker_v2
{
    public enum Language
    {
        RU,
        EN
    }

    public static class Localization
    {
        private static Language _currentLanguage;
        public static event Action LanguageChanged;

        private static readonly Dictionary<string, Dictionary<Language, string>> _strings = new()
        {
            // UI элементы
            ["AppTitle"] = new() { [Language.RU] = "Pikuniku Dance Clicker v2.0", [Language.EN] = "Pikuniku Dance Clicker v2.0" },
            ["BtnStart"] = new() { [Language.RU] = "СТАРТ (Ctrl+Shift+A)", [Language.EN] = "START (Ctrl+Shift+A)" },
            ["BtnStop"] = new() { [Language.RU] = "СТОП (Ctrl+Shift+A)", [Language.EN] = "STOP (Ctrl+Shift+A)" },
            ["StatusReady"] = new() { [Language.RU] = "Готов к работе", [Language.EN] = "Ready" },
            ["HotkeyStartStop"] = new() { [Language.RU] = "Ctrl+Shift+A: Старт/Стоп", [Language.EN] = "Ctrl+Shift+A: Start/Stop" },
            ["HotkeyExit"] = new() { [Language.RU] = "Ctrl+Shift+Q: Выход", [Language.EN] = "Ctrl+Shift+Q: Exit" },
            
            // Статусы
            ["StatusInitCapture"] = new() { [Language.RU] = "Инициализация захвата экрана...", [Language.EN] = "Initializing screen capture..." },
            ["StatusTemplatesLoaded"] = new() { [Language.RU] = "Шаблоны загружены", [Language.EN] = "Templates loaded" },
            ["StatusFindingCenter"] = new() { [Language.RU] = "Поиск центра...", [Language.EN] = "Finding center..." },
            ["StatusCalibrating"] = new() { [Language.RU] = "Калибровка масштаба...", [Language.EN] = "Calibrating scale..." },
            ["StatusWorking"] = new() { [Language.RU] = "Работаем! ROI: {0}x{1}", [Language.EN] = "Working! ROI: {0}x{1}" },
            ["StatusStopped"] = new() { [Language.RU] = "Остановлено", [Language.EN] = "Stopped" },
            
            // Ошибки
            ["ErrorCenterNotFound"] = new() { [Language.RU] = "Ошибка: Центр не найден", [Language.EN] = "Error: Center not found" },
            ["ErrorCalibrationFailed"] = new() { [Language.RU] = "Ошибка: Калибровка не удалась", [Language.EN] = "Error: Calibration failed" },
            ["ErrorCritical"] = new() { [Language.RU] = "Критическая ошибка: {0}", [Language.EN] = "Critical error: {0}" },
            
            // Логи
            ["LogAppStarted"] = new() { [Language.RU] = "=== Приложение запущено ===", [Language.EN] = "=== Application started ===" },
            ["LogAppClosed"] = new() { [Language.RU] = "Приложение закрыто", [Language.EN] = "Application closed" },
            ["LogLanguageChanged"] = new() { [Language.RU] = "Язык изменён на: {0}", [Language.EN] = "Language changed to: {0}" },
            ["LogThreadStarted"] = new() { [Language.RU] = "Рабочий поток запущен с приоритетом Highest", [Language.EN] = "Worker thread started with Highest priority" },
            ["LogThreadStopped"] = new() { [Language.RU] = "Рабочий поток остановлен", [Language.EN] = "Worker thread stopped" },
            ["LogCaptureInit"] = new() { [Language.RU] = "Захват инициализирован: {0}x{1}", [Language.EN] = "Capture initialized: {0}x{1}" },
            ["LogTemplatesLoaded"] = new() { [Language.RU] = "Шаблоны загружены за {0}ms", [Language.EN] = "Templates loaded in {0}ms" },
            ["LogCenterFound"] = new() { [Language.RU] = "✓ Центр найден за {0}ms: ({1}, {2}), точность={3:F3}", [Language.EN] = "✓ Center found in {0}ms: ({1}, {2}), accuracy={3:F3}" },
            ["LogScaleFound"] = new() { [Language.RU] = "✓ Масштаб найден за {0}ms: {1:F2} (точность={2:F3})", [Language.EN] = "✓ Scale found in {0}ms: {1:F2} (accuracy={2:F3})" },
            ["LogTemplatesScaled"] = new() { [Language.RU] = "Шаблоны масштабированы в BGRA за {0}ms", [Language.EN] = "Templates scaled to BGRA in {0}ms" },
            ["LogROISet"] = new() { [Language.RU] = "ROI установлена: {0}x{1} ({2:F2}% экрана)", [Language.EN] = "ROI set: {0}x{1} ({2:F2}% of screen)" },
            ["LogKeyPressed"] = new() { [Language.RU] = "✓ {0} (точность={1:F3}, позиция={2},{3}, время={4}ms)", [Language.EN] = "✓ {0} (conf={1:F3}, pos={2},{3}, time={4}ms)" },
            ["LogStatistics"] = new() { [Language.RU] = "=== Статистика за последние 5 секунд ===", [Language.EN] = "=== Statistics for last 5 seconds ===" },
            ["LogDetections"] = new() { [Language.RU] = "Обнаружения: UP={0}, DOWN={1}, LEFT={2}, RIGHT={3}", [Language.EN] = "Detections: UP={0}, DOWN={1}, LEFT={2}, RIGHT={3}" },
            ["LogFinalStats"] = new() { [Language.RU] = "=== Финальная статистика сессии ({0}) ===", [Language.EN] = "=== Final session statistics ({0}) ===" },
            ["LogFramesProcessed"] = new() { [Language.RU] = "Кадров обработано: {0}", [Language.EN] = "Frames processed: {0}" },
            ["LogAvgFPS"] = new() { [Language.RU] = "Средний FPS: {0:F1}", [Language.EN] = "Average FPS: {0:F1}" },
            ["LogTotalPresses"] = new() { [Language.RU] = "Всего нажатий: {0}", [Language.EN] = "Total presses: {0}" },
            ["LogResourcesCleaned"] = new() { [Language.RU] = "Ресурсы очищены", [Language.EN] = "Resources cleaned" },
            
            // Системная информация
            ["LogResolution"] = new() { [Language.RU] = "Разрешение экрана: {0}x{1}", [Language.EN] = "Screen resolution: {0}x{1}" },
            ["LogDPI"] = new() { [Language.RU] = "Масштаб DPI: {0:F2}", [Language.EN] = "DPI scale: {0:F2}" },
            ["LogProcessors"] = new() { [Language.RU] = "Процессоров: {0}", [Language.EN] = "Processors: {0}" },
            
            // Hotkeys
            ["HotkeyStartStopTriggered"] = new() { [Language.RU] = "Горячая клавиша: Переключение Старт/Стоп", [Language.EN] = "Hotkey: Start/Stop toggle" },
            ["HotkeyExitTriggered"] = new() { [Language.RU] = "Горячая клавиша: Выход из приложения", [Language.EN] = "Hotkey: Exit application" },
        };

        static Localization()
        {
            // Автоопределение языка системы
            var culture = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            _currentLanguage = culture.Equals("ru", StringComparison.OrdinalIgnoreCase) ? Language.RU : Language.EN;
        }

        public static Language CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LanguageChanged?.Invoke();
                }
            }
        }

        public static string Get(string key)
        {
            if (_strings.TryGetValue(key, out var translations))
            {
                if (translations.TryGetValue(_currentLanguage, out var text))
                {
                    return text;
                }
            }
            return $"[{key}]"; // Fallback если ключ не найден
        }

        public static string Get(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        public static void Toggle()
        {
            CurrentLanguage = _currentLanguage == Language.RU ? Language.EN : Language.RU;
        }

        public static string GetLanguageName()
        {
            return _currentLanguage == Language.RU ? "Русский" : "English";
        }

        public static string GetLanguageCode()
        {
            return _currentLanguage == Language.RU ? "RU" : "EN";
        }
    }
}