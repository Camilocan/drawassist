using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DrawAssist.Core
{
    public class SmoothingEngine : IDisposable
    {
        private IntPtr _hookHandle = IntPtr.Zero;
        private WinApi.LowLevelMouseProc? _hookProc;
        private AppSettings _settings;
        private bool _isDrawing = false;

        // Weighted smoothing buffer
        private readonly Queue<PointF> _buffer = new();
        private const int BufferSize = 8;

        // Exponential smoothing state
        private PointF _smoothedPos;
        private bool _firstPoint = true;

        public bool IsActive { get; private set; } = false;

        public SmoothingEngine(AppSettings settings)
        {
            _settings = settings;
        }

        public void UpdateSettings(AppSettings settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            if (_hookHandle != IntPtr.Zero) return;

            _hookProc = HookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _hookHandle = WinApi.SetWindowsHookEx(
                WinApi.WH_MOUSE_LL,
                _hookProc,
                WinApi.GetModuleHandle(curModule.ModuleName),
                0);
            IsActive = true;
        }

        public void Stop()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                WinApi.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            IsActive = false;
            _firstPoint = true;
            _buffer.Clear();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _settings.SmoothingEnabled)
            {
                var hookStruct = Marshal.PtrToStructure<WinApi.MSLLHOOKSTRUCT>(lParam);
                var rawPos = new PointF(hookStruct.pt.X, hookStruct.pt.Y);

                // Only apply smoothing when Photoshop is in focus
                bool psActive = PhotoshopDetector.IsForegroundWindowPhotoshop(
                    _settings.TargetPhotoshopPaths,
                    _settings.ApplyToAllPhotoshop);

                if (!psActive)
                    return WinApi.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

                int msg = (int)wParam;

                if (msg == WinApi.WM_LBUTTONDOWN)
                {
                    _isDrawing = true;
                    _firstPoint = true;
                    _buffer.Clear();
                }
                else if (msg == WinApi.WM_LBUTTONUP)
                {
                    _isDrawing = false;
                    _firstPoint = true;
                    _buffer.Clear();
                }

                if (msg == WinApi.WM_MOUSEMOVE && _isDrawing)
                {
                    var smoothed = ApplySmoothing(rawPos);
                    // Block original event and inject smoothed position
                    WinApi.SetCursorPos((int)smoothed.X, (int)smoothed.Y);
                    return (IntPtr)1; // Block original
                }
            }

            return WinApi.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private PointF ApplySmoothing(PointF raw)
        {
            var preset = GetActivePreset();
            float strength = preset.Strength / 100f;

            return preset.Type switch
            {
                SmoothingType.Weighted => WeightedSmoothing(raw, strength),
                SmoothingType.Exponential => ExponentialSmoothing(raw, strength),
                SmoothingType.CatmullRom => CatmullRomSmoothing(raw, strength),
                _ => raw
            };
        }

        private PointF WeightedSmoothing(PointF raw, float strength)
        {
            _buffer.Enqueue(raw);
            int maxSize = 2 + (int)(strength * (BufferSize - 2));
            while (_buffer.Count > maxSize) _buffer.Dequeue();

            float totalWeight = 0;
            float x = 0, y = 0;
            int i = 1;
            foreach (var p in _buffer)
            {
                float w = i++;
                x += p.X * w;
                y += p.Y * w;
                totalWeight += w;
            }
            return new PointF(x / totalWeight, y / totalWeight);
        }

        private PointF ExponentialSmoothing(PointF raw, float strength)
        {
            float alpha = 1f - (strength * 0.9f); // higher strength = more smoothing = lower alpha
            alpha = Math.Max(0.05f, Math.Min(1f, alpha));

            if (_firstPoint)
            {
                _smoothedPos = raw;
                _firstPoint = false;
                return raw;
            }

            _smoothedPos = new PointF(
                alpha * raw.X + (1 - alpha) * _smoothedPos.X,
                alpha * raw.Y + (1 - alpha) * _smoothedPos.Y);
            return _smoothedPos;
        }

        private PointF CatmullRomSmoothing(PointF raw, float strength)
        {
            _buffer.Enqueue(raw);
            int maxSize = 4;
            while (_buffer.Count > maxSize) _buffer.Dequeue();

            var pts = new List<PointF>(_buffer);
            if (pts.Count < 4) return WeightedSmoothing(raw, strength);

            float t = 0.5f; // midpoint
            var p0 = pts[0]; var p1 = pts[1]; var p2 = pts[2]; var p3 = pts[3];

            float x = 0.5f * ((2 * p1.X) + (-p0.X + p2.X) * t +
                      (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t * t +
                      (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t * t * t);

            float y = 0.5f * ((2 * p1.Y) + (-p0.Y + p2.Y) * t +
                      (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t * t +
                      (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t * t * t);

            // Blend with raw based on inverse strength
            float blend = 1f - strength * 0.7f;
            return new PointF(
                x * (1 - blend) + raw.X * blend,
                y * (1 - blend) + raw.Y * blend);
        }

        private SmoothingPreset GetActivePreset()
        {
            var presets = _settings.Presets;
            int idx = _settings.ActivePresetIndex;
            if (idx >= 0 && idx < presets.Count)
                return presets[idx];
            return new SmoothingPreset();
        }

        public void Dispose() => Stop();
    }
}
