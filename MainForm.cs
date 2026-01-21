using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Pikuniku_dance_clicker_v2
{
    public partial class MainForm : Form
    {
        private ClickerEngine _engine;
        private readonly IConfiguration _configuration;
        private RichTextBox _logBox;
        private Button _btnLanguage;
        private Button _btnStart;
        private Button _btnStop;
        private Label _lblHotkey1;
        private Label _lblHotkey2;
        private bool _isRunning = false;

        // WinAPI для глобальных hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // Hotkey IDs
        private const int HOTKEY_START_STOP = 1;
        private const int HOTKEY_EXIT = 2;

        // Modifiers
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        // Virtual Keys
        private const uint VK_A = 0x41;
        private const uint VK_Q = 0x51;

        public MainForm()
        {
            // ПРАВИЛЬНЫЙ порядок: сначала InitializeComponent создает форму
            InitializeComponent();
            
            _configuration = LoadConfiguration();
            InitializeLogger();
            
            // Подписываемся на смену языка
            Localization.LanguageChanged += OnLanguageChanged;
            
            _engine = new ClickerEngine(UpdateStatus, _configuration);
            
            Log.Information(Localization.Get("LogAppStarted"));
            LogSystemInfo();
            
            // Регистрируем горячие клавиши
            RegisterGlobalHotkeys();
            
            // Применяем язык
            ApplyLocalization();
            
            // Устанавливаем TopMost через WinAPI
            EnsureTopMost();
        }

        private IConfiguration LoadConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("logsettings.json", optional: true, reloadOnChange: true)
                .Build();
        }

        private void InitializeLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(_configuration)
                .CreateLogger();
        }

        private void LogSystemInfo()
        {
            using (var g = this.CreateGraphics())
            {
                float dpiScale = g.DpiX / 96f;
                Log.Information(Localization.Get("LogResolution", Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height));
                Log.Information(Localization.Get("LogDPI", dpiScale));
                Log.Information(Localization.Get("LogProcessors", Environment.ProcessorCount));
            }
        }

        private void RegisterGlobalHotkeys()
        {
            // Ctrl+Shift+A: Start/Stop
            if (!RegisterHotKey(this.Handle, HOTKEY_START_STOP, MOD_CONTROL | MOD_SHIFT, VK_A))
            {
                Log.Warning("Failed to register Ctrl+Shift+A hotkey");
            }
            
            // Ctrl+Shift+Q: Exit
            if (!RegisterHotKey(this.Handle, HOTKEY_EXIT, MOD_CONTROL | MOD_SHIFT, VK_Q))
            {
                Log.Warning("Failed to register Ctrl+Shift+Q hotkey");
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                
                if (id == HOTKEY_START_STOP)
                {
                    Log.Information(Localization.Get("HotkeyStartStopTriggered"));
                    ToggleStartStop();
                }
                else if (id == HOTKEY_EXIT)
                {
                    Log.Information(Localization.Get("HotkeyExitTriggered"));
                    Application.Exit();
                }
            }
            
            base.WndProc(ref m);
        }

        private void EnsureTopMost()
        {
            // Гарантированный TopMost через WinAPI
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, 
                        SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // КРИТИЧНО: AutoScaleMode.Dpi ПЕРЕД созданием контролов
            this.AutoScaleMode = AutoScaleMode.Dpi;
            
            // Темная тема - увеличен размер формы для высоких DPI
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;
            this.ClientSize = new Size(520, 370);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.Name = "MainForm";
            this.Text = Localization.Get("AppTitle");
            this.TopMost = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += MainForm_FormClosing;
            this.Load += MainForm_Load;
            this.Resize += MainForm_Resize;

            // Кнопка смены языка - увеличена для высоких DPI
            _btnLanguage = new Button
            {
                Size = new Size(70, 38),
                Text = Localization.GetLanguageCode(),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Name = "btnLanguage",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(3, 0, 3, 0),
                UseCompatibleTextRendering = false
            };
            _btnLanguage.Location = new Point(this.ClientSize.Width - _btnLanguage.Width - 8, 8);
            _btnLanguage.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            _btnLanguage.Click += BtnLanguage_Click;
            this.Controls.Add(_btnLanguage);

            // Кнопка Старт - увеличена ширина и высота, уменьшен шрифт
            _btnStart = new Button
            {
                Location = new Point(20, 50),
                Size = new Size(235, 55),
                Text = Localization.Get("BtnStart"),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Name = "btnStart",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(5, 3, 5, 3),
                UseCompatibleTextRendering = false
            };
            _btnStart.FlatAppearance.BorderColor = Color.FromArgb(0, 90, 158);
            _btnStart.Click += BtnStart_Click;
            this.Controls.Add(_btnStart);

            // Кнопка Стоп - увеличена ширина и высота, уменьшен шрифт
            _btnStop = new Button
            {
                Location = new Point(265, 50),
                Size = new Size(235, 55),
                Text = Localization.Get("BtnStop"),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(215, 0, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Enabled = false,
                Cursor = Cursors.Hand,
                Name = "btnStop",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(5, 3, 5, 3),
                UseCompatibleTextRendering = false
            };
            _btnStop.FlatAppearance.BorderColor = Color.FromArgb(158, 0, 0);
            _btnStop.Click += BtnStop_Click;
            this.Controls.Add(_btnStop);

            // RichTextBox для цветных логов - увеличена высота
            _logBox = new RichTextBox
            {
                Location = new Point(20, 120),
                Size = new Size(480, 170),
                BackColor = Color.Black,
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Name = "logBox",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(_logBox);

            // Подсказки по горячим клавишам - увеличена высота
            _lblHotkey1 = new Label
            {
                Location = new Point(20, 300),
                Size = new Size(480, 28),
                Text = Localization.Get("HotkeyStartStop"),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Name = "lblHotkey1",
                AutoSize = false,
                Padding = new Padding(0, 5, 0, 0),
                UseCompatibleTextRendering = false
            };
            this.Controls.Add(_lblHotkey1);

            _lblHotkey2 = new Label
            {
                Location = new Point(20, 333),
                Size = new Size(480, 28),
                Text = Localization.Get("HotkeyExit"),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleLeft,
                Name = "lblHotkey2",
                AutoSize = false,
                Padding = new Padding(0, 5, 0, 0),
                UseCompatibleTextRendering = false
            };
            this.Controls.Add(_lblHotkey2);

            // Начальный статус
            AppendLog(Localization.Get("StatusReady"), Color.LightGray);

            this.ResumeLayout(false);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            EnsureTopMost();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Обновляем позицию кнопки языка при изменении размера окна
            if (_btnLanguage != null)
            {
                _btnLanguage.Location = new Point(
                    this.ClientSize.Width - _btnLanguage.Width - 8,
                    8
                );
            }
        }

        private void ApplyLocalization()
        {
            this.Text = Localization.Get("AppTitle");
            if (_btnLanguage != null) _btnLanguage.Text = Localization.GetLanguageCode();
            if (_btnStart != null) _btnStart.Text = Localization.Get("BtnStart");
            if (_btnStop != null) _btnStop.Text = Localization.Get("BtnStop");
            if (_lblHotkey1 != null) _lblHotkey1.Text = Localization.Get("HotkeyStartStop");
            if (_lblHotkey2 != null) _lblHotkey2.Text = Localization.Get("HotkeyExit");
        }

        private void OnLanguageChanged()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnLanguageChanged));
                return;
            }
            
            ApplyLocalization();
            Log.Information(Localization.Get("LogLanguageChanged", Localization.GetLanguageName()));
        }

        private void BtnLanguage_Click(object sender, EventArgs e)
        {
            Localization.Toggle();
        }

        private void ToggleStartStop()
        {
            if (_isRunning)
            {
                BtnStop_Click(null, null);
            }
            else
            {
                BtnStart_Click(null, null);
            }
        }

        private void UpdateStatus(string message, bool isError = false, bool isSuccess = false)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, bool, bool>(UpdateStatus), message, isError, isSuccess);
                return;
            }

            Color color = Color.LightGray;
            if (isError)
                color = Color.FromArgb(255, 100, 100); // Красный
            else if (isSuccess)
                color = Color.FromArgb(100, 255, 100); // Зеленый
            else if (message.Contains("калибр") || message.Contains("Calibrat"))
                color = Color.FromArgb(255, 255, 100); // Желтый

            AppendLog(message, color);
        }

        private void AppendLog(string text, Color color)
        {
            if (_logBox.InvokeRequired)
            {
                _logBox.Invoke(new Action<string, Color>(AppendLog), text, color);
                return;
            }

            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionLength = 0;
            _logBox.SelectionColor = color;
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}\n");
            _logBox.SelectionColor = _logBox.ForeColor;
            _logBox.ScrollToCaret();
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _isRunning = true;

            _engine.Start();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            _engine.Stop();
            
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
            _isRunning = false;
            
            UpdateStatus(Localization.Get("StatusStopped"), false, false);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Отменяем подписку на события
            Localization.LanguageChanged -= OnLanguageChanged;
            
            // Отменяем регистрацию hotkeys
            UnregisterHotKey(this.Handle, HOTKEY_START_STOP);
            UnregisterHotKey(this.Handle, HOTKEY_EXIT);
            
            _engine?.Dispose();
            Log.Information(Localization.Get("LogAppClosed"));
            Log.CloseAndFlush();
        }
    }
}