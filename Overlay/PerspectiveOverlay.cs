using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DrawAssist.Core;

namespace DrawAssist.Overlay
{
    public class PerspectiveOverlay : Form
    {
        private AppSettings _settings;
        private List<PointF> _vanishingPoints = new();
        private int _draggingIndex = -1;
        private const int VpRadius = 10;
        private const int LineCount = 16; // lines per vanishing point

        public PerspectiveOverlay(AppSettings settings)
        {
            _settings = settings;
            InitializeWindow();
            SetDefaultVanishingPoints();
        }

        private void InitializeWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            Opacity = _settings.OverlayOpacity / 100.0;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            AllowTransparency = true;

            // Make the window click-through (pass clicks to Photoshop below)
            SetClickThrough(true);

            Paint += OnPaint;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
        }

        private void SetClickThrough(bool enable)
        {
            const int GWL_EXSTYLE = -20;
            const int WS_EX_LAYERED = 0x80000;
            const int WS_EX_TRANSPARENT = 0x20;

            var style = GetWindowLong(Handle, GWL_EXSTYLE);
            if (enable)
                SetWindowLong(Handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            else
                SetWindowLong(Handle, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void SetDefaultVanishingPoints()
        {
            var w = Screen.PrimaryScreen?.Bounds.Width ?? 1920;
            var h = Screen.PrimaryScreen?.Bounds.Height ?? 1080;

            _vanishingPoints.Clear();
            switch (_settings.PerspectiveMode)
            {
                case PerspectiveMode.OnePoint:
                    _vanishingPoints.Add(new PointF(w / 2f, h / 2f));
                    break;
                case PerspectiveMode.TwoPoint:
                    _vanishingPoints.Add(new PointF(w * 0.15f, h / 2f));
                    _vanishingPoints.Add(new PointF(w * 0.85f, h / 2f));
                    break;
                case PerspectiveMode.ThreePoint:
                    _vanishingPoints.Add(new PointF(w * 0.25f, h * 0.4f));
                    _vanishingPoints.Add(new PointF(w * 0.75f, h * 0.4f));
                    _vanishingPoints.Add(new PointF(w * 0.5f, h * 0.9f));
                    break;
            }
            Invalidate();
        }

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings;
            Opacity = _settings.OverlayOpacity / 100.0;
            SetDefaultVanishingPoints();
        }

        public void SetMode(PerspectiveMode mode)
        {
            _settings.PerspectiveMode = mode;
            SetDefaultVanishingPoints();
        }

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var lineColor = Color.FromArgb(180, _settings.OverlayColor);
            var vpColor = Color.FromArgb(220, Color.White);
            using var linePen = new Pen(lineColor, 1.0f);
            using var vpPen = new Pen(vpColor, 2.0f);
            using var vpBrush = new SolidBrush(Color.FromArgb(120, _settings.OverlayColor));

            var bounds = new RectangleF(0, 0, Width, Height);

            foreach (var vp in _vanishingPoints)
            {
                DrawPerspectiveLines(g, linePen, vp, bounds);
            }

            // Draw vanishing point handles (only visible when not click-through)
            if (_draggingIndex >= 0 || ModifierKeys == Keys.Alt)
            {
                foreach (var vp in _vanishingPoints)
                {
                    g.FillEllipse(vpBrush,
                        vp.X - VpRadius, vp.Y - VpRadius,
                        VpRadius * 2, VpRadius * 2);
                    g.DrawEllipse(vpPen,
                        vp.X - VpRadius, vp.Y - VpRadius,
                        VpRadius * 2, VpRadius * 2);
                }
            }
        }

        private void DrawPerspectiveLines(Graphics g, Pen pen, PointF vp, RectangleF bounds)
        {
            // Draw lines radiating from vanishing point to screen edges
            float angleStep = 180f / LineCount;
            for (int i = 0; i < LineCount; i++)
            {
                float angle = i * angleStep * (float)(Math.PI / 180.0);
                float dx = (float)Math.Cos(angle);
                float dy = (float)Math.Sin(angle);

                // Extend line to screen edge in both directions
                var p1 = ExtendToEdge(vp, dx, dy, bounds);
                var p2 = ExtendToEdge(vp, -dx, -dy, bounds);
                g.DrawLine(pen, p1, p2);
            }
        }

        private static PointF ExtendToEdge(PointF origin, float dx, float dy, RectangleF bounds)
        {
            float t = float.MaxValue;
            if (Math.Abs(dx) > 1e-6)
            {
                float tx = dx > 0 ? (bounds.Right - origin.X) / dx : (bounds.Left - origin.X) / dx;
                if (tx > 0) t = Math.Min(t, tx);
            }
            if (Math.Abs(dy) > 1e-6)
            {
                float ty = dy > 0 ? (bounds.Bottom - origin.Y) / dy : (bounds.Top - origin.Y) / dy;
                if (ty > 0) t = Math.Min(t, ty);
            }
            if (t == float.MaxValue) t = 2000;
            return new PointF(origin.X + dx * t, origin.Y + dy * t);
        }

        // Hold Alt to drag vanishing points
        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (ModifierKeys != Keys.Alt) return;
            SetClickThrough(false);
            for (int i = 0; i < _vanishingPoints.Count; i++)
            {
                var vp = _vanishingPoints[i];
                float dist = (float)Math.Sqrt(
                    Math.Pow(e.X - vp.X, 2) + Math.Pow(e.Y - vp.Y, 2));
                if (dist <= VpRadius * 2)
                {
                    _draggingIndex = i;
                    return;
                }
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (_draggingIndex >= 0)
            {
                _vanishingPoints[_draggingIndex] = new PointF(e.X, e.Y);
                Invalidate();
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            _draggingIndex = -1;
            SetClickThrough(true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000; // WS_EX_LAYERED
                return cp;
            }
        }
    }
}
