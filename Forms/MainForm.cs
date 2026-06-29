using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DrawAssist.Core;

namespace DrawAssist.Forms
{
    public class MainForm : Form
    {
        private AppSettings _settings;
        private Action _onSettingsChanged;

        // Controls
        private CheckBox chkSmoothing = null!;
        private CheckBox chkOverlay = null!;
        private TrackBar trkStrength = null!;
        private Label lblStrength = null!;
        private ComboBox cmbType = null!;
        private ComboBox cmbPresets = null!;
        private Button btnSavePreset = null!;
        private Button btnDeletePreset = null!;
        private TextBox txtPresetName = null!;
        private ComboBox cmbPerspective = null!;
        private TrackBar trkOpacity = null!;
        private Label lblOpacity = null!;
        private Button btnPickColor = null!;
        private Panel pnlColor = null!;
        private CheckBox chkAllPS = null!;
        private CheckedListBox lstPhotoshop = null!;
        private Button btnRefreshPS = null!;
        private Label lblHotkeySmooth = null!;
        private Label lblHotkeyOverlay = null!;
        private List<PhotoshopInstall> _psInstalls = new();

        public MainForm(AppSettings settings, Action onSettingsChanged)
        {
            _settings = settings;
            _onSettingsChanged = onSettingsChanged;
            BuildUI();
            LoadFromSettings();
            RefreshPhotoshopList();
        }

        private void BuildUI()
        {
            Text = "DrawAssist";
            Size = new Size(480, 680);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
            Controls.Add(panel);

            int y = 10;

            // ── SMOOTHING SECTION ──────────────────────────────────
            AddHeader(panel, "Stroke Smoothing", ref y);

            chkSmoothing = AddCheckbox(panel, "Enable Smoothing  (F9 to toggle)", ref y);
            chkSmoothing.CheckedChanged += (s, e) => { _settings.SmoothingEnabled = chkSmoothing.Checked; Notify(); };

            AddLabel(panel, "Active Preset:", ref y);
            cmbPresets = AddCombo(panel, ref y);
            cmbPresets.SelectedIndexChanged += OnPresetSelected;

            var presetRow = new FlowLayoutPanel
            { Location = new Point(0, y), Width = 440, Height = 30, FlowDirection = FlowDirection.LeftToRight };
            txtPresetName = new TextBox { Width = 160, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            btnSavePreset = DarkButton("Save Preset");
            btnSavePreset.Click += OnSavePreset;
            btnDeletePreset = DarkButton("Delete");
            btnDeletePreset.Click += OnDeletePreset;
            presetRow.Controls.AddRange(new Control[] { txtPresetName, btnSavePreset, btnDeletePreset });
            panel.Controls.Add(presetRow);
            y += 36;

            AddLabel(panel, "Smoothing Type:", ref y);
            cmbType = AddCombo(panel, ref y);
            cmbType.Items.AddRange(new object[] { "Weighted Average", "Catmull-Rom Spline", "Exponential" });
            cmbType.SelectedIndexChanged += OnTypeChanged;

            AddLabel(panel, "Strength:", ref y);
            trkStrength = AddSlider(panel, ref y, 0, 100);
            lblStrength = AddValueLabel(panel, ref y, "50%");
            trkStrength.ValueChanged += (s, e) =>
            {
                lblStrength.Text = $"{trkStrength.Value}%";
                GetActivePreset().Strength = trkStrength.Value;
                Notify();
            };

            // ── PERSPECTIVE SECTION ────────────────────────────────
            y += 6;
            AddHeader(panel, "Perspective Overlay", ref y);

            chkOverlay = AddCheckbox(panel, "Enable Overlay  (F10 to toggle)", ref y);
            chkOverlay.CheckedChanged += (s, e) => { _settings.OverlayEnabled = chkOverlay.Checked; Notify(); };

            AddLabel(panel, "Perspective Mode:", ref y);
            cmbPerspective = AddCombo(panel, ref y);
            cmbPerspective.Items.AddRange(new object[] { "1-Point", "2-Point", "3-Point" });
            cmbPerspective.SelectedIndexChanged += (s, e) =>
            {
                _settings.PerspectiveMode = (PerspectiveMode)cmbPerspective.SelectedIndex;
                Notify();
            };

            AddLabel(panel, "Overlay Opacity:", ref y);
            trkOpacity = AddSlider(panel, ref y, 10, 100);
            lblOpacity = AddValueLabel(panel, ref y, "60%");
            trkOpacity.ValueChanged += (s, e) =>
            {
                lblOpacity.Text = $"{trkOpacity.Value}%";
                _settings.OverlayOpacity = trkOpacity.Value;
                Notify();
            };

            AddLabel(panel, "Line Color:", ref y);
            pnlColor = new Panel
            {
                Location = new Point(0, y), Size = new Size(30, 22),
                BackColor = _settings.OverlayColor, BorderStyle = BorderStyle.FixedSingle
            };
            btnPickColor = DarkButton("Pick Color");
            btnPickColor.Location = new Point(36, y - 2);
            btnPickColor.Click += OnPickColor;
            panel.Controls.Add(pnlColor);
            panel.Controls.Add(btnPickColor);
            y += 32;

            var hintLabel = new Label
            {
                Text = "Tip: Hold Alt and drag the glowing dots to move vanishing points.",
                Location = new Point(0, y), Width = 440, Height = 34,
                ForeColor = Color.FromArgb(160, 160, 160), Font = new Font("Segoe UI", 8f)
            };
            panel.Controls.Add(hintLabel);
            y += 38;

            // ── PHOTOSHOP TARGETS ──────────────────────────────────
            AddHeader(panel, "Target Photoshop Versions", ref y);

            chkAllPS = AddCheckbox(panel, "Apply to all Photoshop versions", ref y);
            chkAllPS.CheckedChanged += (s, e) =>
            {
                _settings.ApplyToAllPhotoshop = chkAllPS.Checked;
                lstPhotoshop.Enabled = !chkAllPS.Checked;
                Notify();
            };

            lstPhotoshop = new CheckedListBox
            {
                Location = new Point(0, y), Width = 440, Height = 90,
                BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, CheckOnClick = true
            };
            lstPhotoshop.ItemCheck += OnPsItemCheck;
            panel.Controls.Add(lstPhotoshop);
            y += 96;

            btnRefreshPS = DarkButton("Refresh List");
            btnRefreshPS.Location = new Point(0, y);
            btnRefreshPS.Click += (s, e) => RefreshPhotoshopList();
            panel.Controls.Add(btnRefreshPS);
            y += 36;

            // ── HOTKEYS INFO ───────────────────────────────────────
            AddHeader(panel, "Hotkeys", ref y);
            lblHotkeySmooth = AddLabel(panel, "Toggle Smoothing: F9", ref y);
            lblHotkeyOverlay = AddLabel(panel, "Toggle Overlay: F10", ref y);

            panel.Height = y + 20;
        }

        // ── HELPERS ────────────────────────────────────────────────

        private void AddHeader(Panel p, string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text, Location = new Point(0, y), Width = 440, Height = 22,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255)
            };
            p.Controls.Add(lbl);
            y += 26;
        }

        private Label AddLabel(Panel p, string text, ref int y)
        {
            var lbl = new Label { Text = text, Location = new Point(0, y), Width = 440, Height = 18, ForeColor = Color.Silver };
            p.Controls.Add(lbl);
            y += 20;
            return lbl;
        }

        private CheckBox AddCheckbox(Panel p, string text, ref int y)
        {
            var chk = new CheckBox { Text = text, Location = new Point(0, y), Width = 440, Height = 22, ForeColor = Color.White };
            p.Controls.Add(chk);
            y += 26;
            return chk;
        }

        private ComboBox AddCombo(Panel p, ref int y)
        {
            var cmb = new ComboBox
            {
                Location = new Point(0, y), Width = 440, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            p.Controls.Add(cmb);
            y += 28;
            return cmb;
        }

        private TrackBar AddSlider(Panel p, ref int y, int min, int max)
        {
            var trk = new TrackBar { Location = new Point(0, y), Width = 440, Minimum = min, Maximum = max, TickFrequency = 10 };
            p.Controls.Add(trk);
            y += 36;
            return trk;
        }

        private Label AddValueLabel(Panel p, ref int y, string text)
        {
            var lbl = new Label { Text = text, Location = new Point(0, y), Width = 60, Height = 18, ForeColor = Color.White };
            p.Controls.Add(lbl);
            y += 22;
            return lbl;
        }

        private Button DarkButton(string text)
        {
            return new Button
            {
                Text = text, Height = 26, Width = 100, Margin = new Padding(0, 0, 6, 0),
                BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
        }

        // ── LOAD / SAVE ────────────────────────────────────────────

        private void LoadFromSettings()
        {
            chkSmoothing.Checked = _settings.SmoothingEnabled;
            chkOverlay.Checked = _settings.OverlayEnabled;
            trkOpacity.Value = Math.Clamp(_settings.OverlayOpacity, 10, 100);
            lblOpacity.Text = $"{_settings.OverlayOpacity}%";
            pnlColor.BackColor = _settings.OverlayColor;
            cmbPerspective.SelectedIndex = (int)_settings.PerspectiveMode;
            chkAllPS.Checked = _settings.ApplyToAllPhotoshop;
            lstPhotoshop.Enabled = !_settings.ApplyToAllPhotoshop;
            RefreshPresetList();
        }

        private void RefreshPresetList()
        {
            cmbPresets.Items.Clear();
            foreach (var p in _settings.Presets)
                cmbPresets.Items.Add(p.Name);

            int idx = Math.Clamp(_settings.ActivePresetIndex, 0, _settings.Presets.Count - 1);
            if (cmbPresets.Items.Count > 0)
                cmbPresets.SelectedIndex = idx;
        }

        private void OnPresetSelected(object? sender, EventArgs e)
        {
            int idx = cmbPresets.SelectedIndex;
            if (idx < 0 || idx >= _settings.Presets.Count) return;
            _settings.ActivePresetIndex = idx;
            var preset = _settings.Presets[idx];
            txtPresetName.Text = preset.Name;
            cmbType.SelectedIndex = (int)preset.Type;
            trkStrength.Value = Math.Clamp(preset.Strength, 0, 100);
            lblStrength.Text = $"{preset.Strength}%";
            Notify();
        }

        private void OnTypeChanged(object? sender, EventArgs e)
        {
            GetActivePreset().Type = (SmoothingType)cmbType.SelectedIndex;
            Notify();
        }

        private void OnSavePreset(object? sender, EventArgs e)
        {
            string name = txtPresetName.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = "Preset";

            int idx = cmbPresets.SelectedIndex;
            if (idx >= 0 && idx < _settings.Presets.Count)
            {
                _settings.Presets[idx].Name = name;
            }
            else
            {
                _settings.Presets.Add(new SmoothingPreset
                {
                    Name = name,
                    Type = (SmoothingType)cmbType.SelectedIndex,
                    Strength = trkStrength.Value
                });
                _settings.ActivePresetIndex = _settings.Presets.Count - 1;
            }
            RefreshPresetList();
            SettingsManager.Save(_settings);
            Notify();
        }

        private void OnDeletePreset(object? sender, EventArgs e)
        {
            int idx = cmbPresets.SelectedIndex;
            if (idx < 0 || _settings.Presets.Count <= 1) return;
            _settings.Presets.RemoveAt(idx);
            _settings.ActivePresetIndex = Math.Max(0, idx - 1);
            RefreshPresetList();
            Notify();
        }

        private void OnPickColor(object? sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _settings.OverlayColor };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _settings.OverlayColor = dlg.Color;
                pnlColor.BackColor = dlg.Color;
                Notify();
            }
        }

        private void RefreshPhotoshopList()
        {
            _psInstalls = PhotoshopDetector.FindInstalled();
            lstPhotoshop.Items.Clear();
            foreach (var ps in _psInstalls)
            {
                bool isSelected = _settings.TargetPhotoshopPaths.Contains(ps.ExePath);
                lstPhotoshop.Items.Add(ps.ToString(), isSelected);
            }
            if (_psInstalls.Count == 0)
                lstPhotoshop.Items.Add("No Photoshop installations found");
        }

        private void OnPsItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (e.Index >= _psInstalls.Count) return;
            var path = _psInstalls[e.Index].ExePath;
            if (e.NewValue == CheckState.Checked)
            {
                if (!_settings.TargetPhotoshopPaths.Contains(path))
                    _settings.TargetPhotoshopPaths.Add(path);
            }
            else
            {
                _settings.TargetPhotoshopPaths.Remove(path);
            }
            Notify();
        }

        private SmoothingPreset GetActivePreset()
        {
            int idx = _settings.ActivePresetIndex;
            if (idx >= 0 && idx < _settings.Presets.Count)
                return _settings.Presets[idx];
            return new SmoothingPreset();
        }

        private void Notify()
        {
            SettingsManager.Save(_settings);
            _onSettingsChanged?.Invoke();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide(); // Keep running in tray
            }
            base.OnFormClosing(e);
        }
    }
}
