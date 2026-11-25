using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;

namespace ViewCubeOverlay
{
  /// <summary>
  /// Настройки видового куба. Без System.Text.Json — совместимость с runtime Renga Net8.
  /// Файл: %AppData%\ViewCubeOverlay\settings.txt
  /// </summary>
  public sealed class ViewCubeSettings
  {
    public const double MinScale = 0.7;
    public const double MaxScale = 1.8;
    public const double DefaultScale = 1.0;
    public const double ScaleStep = 0.1;
    public const double BaseWindowWidth = 196;
    public const double BaseWindowHeight = 220;

    public const string PresetClassic = "Classic";
    public const string PresetNeutral = "Neutral";
    public const string PresetContrast = "Contrast";
    public const string PresetRenga = "Renga";
    public const string PresetCustom = "Custom";

    public const string ShapeSharp = "Sharp";
    public const string ShapeChamfered = "Chamfered";

    private double _cubeScale = DefaultScale;

    public bool UserWantsWindow { get; set; }
    public bool UserMovedWindow { get; set; }
    public double DockOffsetX { get; set; }
    public double DockOffsetY { get; set; }

    public double CubeScale
    {
      get { return _cubeScale; }
      set { _cubeScale = ClampScale(value); }
    }

    public string StylePreset { get; set; } = PresetClassic;
    public string CubeShape { get; set; } = ShapeSharp;

    public Color FaceTop { get; set; }
    public Color FaceBottom { get; set; }
    public Color FaceFront { get; set; }
    public Color FaceBack { get; set; }
    public Color FaceRight { get; set; }
    public Color FaceLeft { get; set; }

    public Color LabelText { get; set; }
    public Color LabelPill { get; set; }
    public bool ShowLabels { get; set; } = true;

    public Color HotspotColor { get; set; }
    public Color HoverColor { get; set; }
    public Color ActiveFaceColor { get; set; }

    public bool CompactChrome { get; set; }

    public ViewCubeSettings()
    {
      ApplyPreset(PresetClassic, markPreset: true);
    }

    public static string SettingsDirectory =>
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ViewCubeOverlay");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.txt");

    public double WindowWidth => BaseWindowWidth * CubeScale;
    public double WindowHeight => BaseWindowHeight * CubeScale;

    public static double ClampScale(double value)
    {
      if (double.IsNaN(value) || double.IsInfinity(value))
        return DefaultScale;
      if (value < MinScale)
        return MinScale;
      if (value > MaxScale)
        return MaxScale;
      return Math.Round(value, 2);
    }

    public bool ChangeScaleBySteps(int steps)
    {
      var next = ClampScale(CubeScale + steps * ScaleStep);
      if (Math.Abs(next - CubeScale) < 0.001)
        return false;
      CubeScale = next;
      return true;
    }

    public static ViewCubeSettings Load()
    {
      var result = new ViewCubeSettings();
      try
      {
        var legacyJson = Path.Combine(SettingsDirectory, "settings.json");
        if (File.Exists(SettingsPath))
        {
          bool sawCubeShape = false;
          foreach (var line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
          {
            var parts = line.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
              continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            if (string.Equals(key, "CubeShape", StringComparison.OrdinalIgnoreCase))
              sawCubeShape = true;
            ApplyKey(result, key, value);
          }

          if (!sawCubeShape && string.Equals(result.StylePreset, PresetRenga, StringComparison.OrdinalIgnoreCase))
            result.CubeShape = ShapeChamfered;
        }
        else if (File.Exists(legacyJson))
        {
          var json = File.ReadAllText(legacyJson, Encoding.UTF8);
          result.UserWantsWindow = json.IndexOf("\"userWantsWindow\": true", StringComparison.OrdinalIgnoreCase) >= 0
                                   || json.IndexOf("\"userWantsWindow\":true", StringComparison.OrdinalIgnoreCase) >= 0;
          result.UserMovedWindow = json.IndexOf("\"userMovedWindow\": true", StringComparison.OrdinalIgnoreCase) >= 0
                                   || json.IndexOf("\"userMovedWindow\":true", StringComparison.OrdinalIgnoreCase) >= 0;
          result.DockOffsetX = ExtractJsonNumber(json, "dockOffsetX");
          result.DockOffsetY = ExtractJsonNumber(json, "dockOffsetY");
          result.Save();
        }
      }
      catch
      {
        return new ViewCubeSettings();
      }

      return result;
    }

    public void Save()
    {
      try
      {
        Directory.CreateDirectory(SettingsDirectory);
        var sb = new StringBuilder();
        sb.AppendLine("UserWantsWindow=" + (UserWantsWindow ? "true" : "false"));
        sb.AppendLine("UserMovedWindow=" + (UserMovedWindow ? "true" : "false"));
        sb.AppendLine("DockOffsetX=" + DockOffsetX.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("DockOffsetY=" + DockOffsetY.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("CubeScale=" + CubeScale.ToString("0.00", CultureInfo.InvariantCulture));
        sb.AppendLine("StylePreset=" + (StylePreset ?? PresetClassic));
        sb.AppendLine("CubeShape=" + (CubeShape ?? ShapeSharp));
        sb.AppendLine("FaceTop=" + ColorToHex(FaceTop));
        sb.AppendLine("FaceBottom=" + ColorToHex(FaceBottom));
        sb.AppendLine("FaceFront=" + ColorToHex(FaceFront));
        sb.AppendLine("FaceBack=" + ColorToHex(FaceBack));
        sb.AppendLine("FaceRight=" + ColorToHex(FaceRight));
        sb.AppendLine("FaceLeft=" + ColorToHex(FaceLeft));
        sb.AppendLine("LabelText=" + ColorToHex(LabelText));
        sb.AppendLine("LabelPill=" + ColorToHex(LabelPill));
        sb.AppendLine("ShowLabels=" + (ShowLabels ? "true" : "false"));
        sb.AppendLine("HotspotColor=" + ColorToHex(HotspotColor));
        sb.AppendLine("HoverColor=" + ColorToHex(HoverColor));
        sb.AppendLine("ActiveFaceColor=" + ColorToHex(ActiveFaceColor));
        sb.AppendLine("CompactChrome=" + (CompactChrome ? "true" : "false"));
        File.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
      }
      catch
      {
        // Настройки необязательны.
      }
    }

    public void ResetDock()
    {
      UserMovedWindow = false;
      DockOffsetX = 0;
      DockOffsetY = 0;
      Save();
    }

    public void ApplyPreset(string preset)
    {
      ApplyPreset(preset, markPreset: true);
    }

    public static string ColorToHex(Color color)
    {
      if (color.A == 255)
        return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
      return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", color.A, color.R, color.G, color.B);
    }

    public static Color ParseColor(string value, Color fallback)
    {
      if (string.IsNullOrWhiteSpace(value))
        return fallback;

      value = value.Trim();
      try
      {
        if (value[0] == '#' && (value.Length == 4 || value.Length == 7 || value.Length == 9))
        {
          if (value.Length == 4)
          {
            var r = Uri.FromHex(value[1]);
            var g = Uri.FromHex(value[2]);
            var b = Uri.FromHex(value[3]);
            return Color.FromRgb((byte)(r * 17), (byte)(g * 17), (byte)(b * 17));
          }

          if (value.Length == 7)
          {
            return Color.FromRgb(
              Convert.ToByte(value.Substring(1, 2), 16),
              Convert.ToByte(value.Substring(3, 2), 16),
              Convert.ToByte(value.Substring(5, 2), 16));
          }

          return Color.FromArgb(
            Convert.ToByte(value.Substring(1, 2), 16),
            Convert.ToByte(value.Substring(3, 2), 16),
            Convert.ToByte(value.Substring(5, 2), 16),
            Convert.ToByte(value.Substring(7, 2), 16));
        }

        var converted = ColorConverter.ConvertFromString(value);
        if (converted is Color color)
          return color;
      }
      catch
      {
      }

      return fallback;
    }

    private void ApplyPreset(string preset, bool markPreset)
    {
      var name = NormalizePreset(preset);
      if (name == PresetNeutral)
      {
        CubeShape = ShapeSharp;
        FaceTop = Color.FromRgb(208, 212, 216);
        FaceBottom = Color.FromRgb(90, 94, 98);
        FaceFront = Color.FromRgb(168, 174, 180);
        FaceBack = Color.FromRgb(122, 128, 136);
        FaceRight = Color.FromRgb(196, 200, 204);
        FaceLeft = Color.FromRgb(144, 150, 156);
        LabelText = Colors.White;
        LabelPill = Color.FromArgb(192, 16, 18, 24);
        HotspotColor = Color.FromArgb(224, 255, 255, 255);
        HoverColor = Color.FromRgb(240, 208, 128);
        ActiveFaceColor = Color.FromRgb(106, 160, 208);
      }
      else if (name == PresetContrast)
      {
        CubeShape = ShapeSharp;
        FaceTop = Color.FromRgb(244, 244, 244);
        FaceBottom = Color.FromRgb(26, 26, 26);
        FaceFront = Color.FromRgb(30, 106, 255);
        FaceBack = Color.FromRgb(34, 38, 44);
        FaceRight = Color.FromRgb(224, 32, 32);
        FaceLeft = Color.FromRgb(240, 192, 0);
        LabelText = Colors.White;
        LabelPill = Color.FromArgb(224, 0, 0, 0);
        HotspotColor = Color.FromArgb(255, 255, 210, 74);
        HoverColor = Color.FromRgb(255, 225, 74);
        ActiveFaceColor = Color.FromRgb(0, 212, 255);
      }
      else if (name == PresetRenga)
      {
        // Навигационный куб Renga: фаски, светло-серый, тёмные рёбра, без плашки.
        CubeShape = ShapeChamfered;
        FaceTop = Color.FromRgb(236, 237, 239);
        FaceBottom = Color.FromRgb(206, 208, 212);
        FaceFront = Color.FromRgb(226, 228, 230);
        FaceBack = Color.FromRgb(216, 218, 222);
        FaceRight = Color.FromRgb(230, 232, 234);
        FaceLeft = Color.FromRgb(220, 222, 226);
        LabelText = Color.FromRgb(64, 68, 74);
        LabelPill = Color.FromArgb(0, 0, 0, 0);
        HotspotColor = Color.FromRgb(222, 224, 228);
        HoverColor = Color.FromRgb(255, 232, 186);
        ActiveFaceColor = Color.FromRgb(214, 220, 228);
      }
      else
      {
        // Классика: текущий steel-blue / indian-red / gainsboro.
        CubeShape = ShapeSharp;
        FaceTop = Color.FromRgb(148, 158, 168);
        FaceBottom = Color.FromRgb(72, 74, 80);
        FaceFront = Color.FromRgb(46, 110, 168);
        FaceBack = Color.FromRgb(70, 82, 92);
        FaceRight = Color.FromRgb(176, 72, 68);
        FaceLeft = Color.FromRgb(140, 92, 48);
        LabelText = Colors.White;
        LabelPill = Color.FromArgb(190, 8, 10, 16);
        HotspotColor = Color.FromArgb(210, 248, 248, 252);
        HoverColor = Color.FromRgb(232, 176, 72);
        ActiveFaceColor = Color.FromRgb(72, 154, 230);
        name = PresetClassic;
      }

      if (markPreset)
        StylePreset = name;
    }

    public bool IsChamferedShape()
    {
      return string.Equals(CubeShape, ShapeChamfered, StringComparison.OrdinalIgnoreCase)
             || string.Equals(StylePreset, PresetRenga, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePreset(string preset)
    {
      if (string.IsNullOrWhiteSpace(preset))
        return PresetClassic;
      if (string.Equals(preset, PresetNeutral, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(preset, "Нейтральный", StringComparison.OrdinalIgnoreCase))
        return PresetNeutral;
      if (string.Equals(preset, PresetContrast, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(preset, "Контраст", StringComparison.OrdinalIgnoreCase))
        return PresetContrast;
      if (string.Equals(preset, PresetRenga, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(preset, "Навигационный", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(preset, "Navigational", StringComparison.OrdinalIgnoreCase))
        return PresetRenga;
      if (string.Equals(preset, PresetCustom, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(preset, "Свой", StringComparison.OrdinalIgnoreCase))
        return PresetCustom;
      return PresetClassic;
    }

    private static string NormalizeShape(string shape)
    {
      if (string.IsNullOrWhiteSpace(shape))
        return ShapeSharp;
      if (string.Equals(shape, ShapeChamfered, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(shape, "Chamfer", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(shape, "Фасетный", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(shape, "Навигационный", StringComparison.OrdinalIgnoreCase))
        return ShapeChamfered;
      return ShapeSharp;
    }

    private static void ApplyKey(ViewCubeSettings result, string key, string value)
    {
      switch (key)
      {
        case "UserWantsWindow":
          result.UserWantsWindow = ParseBool(value);
          break;
        case "UserMovedWindow":
          result.UserMovedWindow = ParseBool(value);
          break;
        case "DockOffsetX":
          result.DockOffsetX = ParseDouble(value);
          break;
        case "DockOffsetY":
          result.DockOffsetY = ParseDouble(value);
          break;
        case "CubeScale":
          result.CubeScale = ParseDouble(value, DefaultScale);
          break;
        case "StylePreset":
          result.StylePreset = NormalizePreset(value);
          break;
        case "CubeShape":
          result.CubeShape = NormalizeShape(value);
          break;
        case "FaceTop":
          result.FaceTop = ParseColor(value, result.FaceTop);
          break;
        case "FaceBottom":
          result.FaceBottom = ParseColor(value, result.FaceBottom);
          break;
        case "FaceFront":
          result.FaceFront = ParseColor(value, result.FaceFront);
          break;
        case "FaceBack":
          result.FaceBack = ParseColor(value, result.FaceBack);
          break;
        case "FaceRight":
          result.FaceRight = ParseColor(value, result.FaceRight);
          break;
        case "FaceLeft":
          result.FaceLeft = ParseColor(value, result.FaceLeft);
          break;
        case "LabelText":
          result.LabelText = ParseColor(value, result.LabelText);
          break;
        case "LabelPill":
          result.LabelPill = ParseColor(value, result.LabelPill);
          break;
        case "ShowLabels":
          result.ShowLabels = ParseBool(value, true);
          break;
        case "HotspotColor":
          result.HotspotColor = ParseColor(value, result.HotspotColor);
          break;
        case "HoverColor":
          result.HoverColor = ParseColor(value, result.HoverColor);
          break;
        case "ActiveFaceColor":
          result.ActiveFaceColor = ParseColor(value, result.ActiveFaceColor);
          break;
        case "CompactChrome":
          result.CompactChrome = ParseBool(value);
          break;
      }
    }

    private static bool ParseBool(string value, bool fallback = false)
    {
      if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
        return true;
      if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0")
        return false;
      return fallback;
    }

    private static double ParseDouble(string value)
    {
      return ParseDouble(value, 0);
    }

    private static double ParseDouble(string value, double fallback)
    {
      return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;
    }

    private static double ExtractJsonNumber(string json, string name)
    {
      var key = "\"" + name + "\"";
      var idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
      if (idx < 0)
        return 0;
      idx = json.IndexOf(':', idx);
      if (idx < 0)
        return 0;
      var end = idx + 1;
      while (end < json.Length && (char.IsWhiteSpace(json[end]) || json[end] == ':'))
        end++;
      var start = end;
      while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.' || json[end] == 'e' || json[end] == 'E' || json[end] == '+'))
        end++;
      if (end <= start)
        return 0;
      return ParseDouble(json.Substring(start, end - start));
    }
  }
}
