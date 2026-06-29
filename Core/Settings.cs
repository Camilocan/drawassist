using System.Collections.Generic;

namespace DrawAssist.Core
{
    public enum SmoothingType
    {
        Weighted,       // Weighted average - natural feel
        CatmullRom,     // Catmull-Rom spline - smooth curves
        Exponential     // Exponential decay - responsive with tail
    }

    public enum PerspectiveMode
    {
        OnePoint,
        TwoPoint,
        ThreePoint
    }

    public class SmoothingPreset
    {
        public string Name { get; set; } = "New Preset";
        public SmoothingType Type { get; set; } = SmoothingType.Weighted;
        public int Strength { get; set; } = 50; // 0-100
    }

    public class AppSettings
    {
        public bool SmoothingEnabled { get; set; } = false;
        public bool OverlayEnabled { get; set; } = false;
        public int ActivePresetIndex { get; set; } = 0;
        public List<SmoothingPreset> Presets { get; set; } = new()
        {
            new SmoothingPreset { Name = "Light", Type = SmoothingType.Weighted, Strength = 25 },
            new SmoothingPreset { Name = "Medium", Type = SmoothingType.Weighted, Strength = 50 },
            new SmoothingPreset { Name = "Heavy", Type = SmoothingType.CatmullRom, Strength = 80 },
        };
        public List<string> TargetPhotoshopPaths { get; set; } = new();
        public bool ApplyToAllPhotoshop { get; set; } = true;
        public PerspectiveMode PerspectiveMode { get; set; } = PerspectiveMode.TwoPoint;
        public int OverlayOpacity { get; set; } = 60;
        public System.Drawing.Color OverlayColor { get; set; } = System.Drawing.Color.Cyan;

        // Hotkeys
        public Keys ToggleSmoothingKey { get; set; } = Keys.F9;
        public Keys ToggleOverlayKey { get; set; } = Keys.F10;
    }
}
