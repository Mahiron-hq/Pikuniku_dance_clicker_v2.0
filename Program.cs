using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pikuniku_dance_clicker_v2
{
    static class Program
    {
        // DPI Awareness для корректной работы при любом масштабе Windows
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        static extern int SetProcessDpiAwareness(int value);

        // PROCESS_PER_MONITOR_DPI_AWARE
        private const int PROCESS_PER_MONITOR_DPI_AWARE = 2;

        [STAThread]
        static void Main()
        {
            // КРИТИЧНО: Устанавливаем режим High DPI ПЕРЕД всеми другими вызовами
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // Устанавливаем DPI Awareness для Windows 8.1+
            try
            {
                SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE);
            }
            catch
            {
                // Fallback для Windows 7/8
                try
                {
                    SetProcessDPIAware();
                }
                catch
                {
                    // Игнорируем ошибки DPI awareness
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}