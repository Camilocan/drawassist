using System;
using System.Drawing;
using System.Windows.Forms;
using DrawAssist.Core;
using DrawAssist.Forms;
using DrawAssist.Overlay;

namespace DrawAssist
{
    public class AppController : IDisposable
    {
        private AppSettings _settings;
        private SmoothingEngine _smoother;
        private PerspectiveOverlay? _overlay;
        private MainForm? _mainForm;
        private NotifyIcon _trayIcon;
        private HotkeyWindow _hotkeyWin;

        private const int HOTKEY_SMOOTH = 1;
        private const int HOTKEY_OVERLAY = 2;

        public AppController()
        {
            _settings = SettingsManager.Load();
            _smoother = new SmoothingEngine(_settings);
            _trayIcon = BuildTrayIcon();
            _hotkeyWin = new HotkeyWindow();
            _hotkeyWin.HotkeyPressed += OnHotkey;
            RegisterHotkeys();
        }

        public void Run()
        {
            if (_settings.SmoothingEnabled) _smoother.Start();
            if (_settings.OverlayEnabled) ShowOverlay();
            UpdateTrayMenu();
        }

        // ── TRAY ICON ──────────────────────────────────────────────

        private NotifyIcon BuildTrayIcon()
        {
            var icon = new NotifyIcon
            {
                Text = "DrawAssist",
                Icon = SystemIcons.Application,
                Visible = true
            };
            icon.DoubleClick += (s, e) => OpenMainForm();
            return icon;
        }

        private void UpdateTrayMenu()
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Open DrawAssist", null, (s, e) => OpenMainForm());
            menu.Items.Add(new ToolStripSeparator());

            var smoothItem = new ToolStripMenuItem("Smoothing: " + (_settings.SmoothingEnabled ? "ON" : "OFF"));
            smoothItem.Click += (s, e) => ToggleSmoothing();
            menu.Items.Add(smoothItem);

            var overlayItem = new ToolStripMenuItem("Overlay: " + (_settings.OverlayEnabled ? "ON" : "OFF"));
            overlayItem.Click += (s, e) => ToggleOverlay();
            menu.Items.Add(overlayItem);

            menu.Items.Add(new ToolStripSeparator());

            // Quick preset switcher
            var presetMenu = new ToolStripMenuItem("Smoothing Preset");
            for (int i = 0; i < _settings.Presets.Count; i++)
            {
                int captured = i;
                var item = new ToolStripMenuItem(_settings.Presets[i].Name);
                item.Checked = (i == _settings.ActivePresetIndex);
                item.Click += (s, e) =>
                {
                    _settings.ActivePresetIndex = captured;
                    _smoother.UpdateSettings(_settings);
                    SettingsManager.Save(_settings);
                    UpdateTrayMenu();
                };
                presetMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(presetMenu);

            // Quick perspective mode switcher
            var perspMenu = new ToolStripMenuItem("Perspective Mode");
            string[] modes = { "1-Point", "2-Point", "3-Point" };
            for (int i = 0; i < modes.Length; i++)
            {
                int captured = i;
                var item = new ToolStripMenuItem(modes[i]);
                item.Checked = ((int)_settings.PerspectiveMode == i);
                item.Click += (s, e) =>
                {
                    _settings.PerspectiveMode = (PerspectiveMode)captured;
                    _overlay?.SetMode(_settings.PerspectiveMode);
                    SettingsManager.Save(_settings);
                    UpdateTrayMenu();
                };
                perspMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(perspMenu);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitApp());

            _trayIcon.ContextMenuStrip = menu;
        }

        // ── FEATURES ───────────────────────────────────────────────

        private void ToggleSmoothing()
        {
            _settings.SmoothingEnabled = !_settings.SmoothingEnabled;
            if (_settings.SmoothingEnabled) _smoother.Start();
            else _smoother.Stop();
            SettingsManager.Save(_settings);
            UpdateTrayMenu();
            ShowBalloon("Smoothing " + (_settings.SmoothingEnabled ? "ON" : "OFF"));
        }

        private void ToggleOverlay()
        {
            _settings.OverlayEnabled = !_settings.OverlayEnabled;
            if (_settings.OverlayEnabled) ShowOverlay();
            else HideOverlay();
            SettingsManager.Save(_settings);
            UpdateTrayMenu();
            ShowBalloon("Overlay " + (_settings.OverlayEnabled ? "ON" : "OFF"));
        }

        private void ShowOverlay()
        {
            if (_overlay == null || _overlay.IsDisposed)
            {
                _overlay = new PerspectiveOverlay(_settings);
                _overlay.Show();
            }
            else
            {
                _overlay.ApplySettings(_settings);
                _overlay.Show();
            }
        }

        private void HideOverlay()
        {
            _overlay?.Hide();
        }

        private void OpenMainForm()
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm(_settings, OnSettingsChanged);
            }
            _mainForm.Show();
            _mainForm.BringToFront();
        }

        private void OnSettingsChanged()
        {
            _smoother.UpdateSettings(_settings);

            if (_settings.SmoothingEnabled && !_smoother.IsActive) _smoother.Start();
            else if (!_settings.SmoothingEnabled && _smoother.IsActive) _smoother.Stop();

            if (_settings.OverlayEnabled) ShowOverlay();
            else HideOverlay();

            UpdateTrayMenu();
        }

        // ── HOTKEYS ────────────────────────────────────────────────

        private void RegisterHotkeys()
        {
            WinApi.RegisterHotKey(_hotkeyWin.Handle, HOTKEY_SMOOTH, WinApi.MOD_NONE, (uint)_settings.ToggleSmoothingKey);
            WinApi.RegisterHotKey(_hotkeyWin.Handle, HOTKEY_OVERLAY, WinApi.MOD_NONE, (uint)_settings.ToggleOverlayKey);
        }

        private void OnHotkey(int id)
        {
            if (id == HOTKEY_SMOOTH) ToggleSmoothing();
            else if (id == HOTKEY_OVERLAY) ToggleOverlay();
        }

        private void ShowBalloon(string message)
        {
            _trayIcon.ShowBalloonTip(1500, "DrawAssist", message, ToolTipIcon.Info);
        }

        private void ExitApp()
        {
            _smoother.Stop();
            HideOverlay();
            _trayIcon.Visible = false;
            WinApi.UnregisterHotKey(_hotkeyWin.Handle, HOTKEY_SMOOTH);
            WinApi.UnregisterHotKey(_hotkeyWin.Handle, HOTKEY_OVERLAY);
            Application.Exit();
        }

        public void Dispose()
        {
            _smoother.Dispose();
            _trayIcon.Dispose();
            _hotkeyWin.Dispose();
        }
    }

    // Minimal invisible window just to receive WM_HOTKEY messages
    public class HotkeyWindow : NativeWindow, IDisposable
    {
        public event Action<int>? HotkeyPressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WinApi.WM_HOTKEY)
                HotkeyPressed?.Invoke(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }
}
