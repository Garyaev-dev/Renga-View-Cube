using System.IO;
using System.Windows.Controls;
using System.Xml.Linq;
using ViewCubeOverlay;

internal static class Program
{
  [STAThread]
  private static int Main(string[] args)
  {
    try
    {
      if (args.Length != 1)
        throw new ArgumentException("Pass the path to ViewCubeOverlay.rndesc.");

      var expectedVersion = XDocument.Load(args[0]).Root?.Element("Version")?.Value;
      var assemblyVersion = typeof(SettingsWindow).Assembly.GetName().Version?.ToString();
      Check(!string.IsNullOrWhiteSpace(expectedVersion), "Plugin descriptor has no version.");
      Check(assemblyVersion == expectedVersion, $"Assembly version {assemblyVersion} differs from descriptor {expectedVersion}.");

      var settingsPath = ViewCubeSettings.SettingsPath;
      var before = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
      var settings = new ViewCubeSettings { CubeScale = 1.3, ShowLabels = false };
      var window = new SettingsWindow(settings);
      var versionText = (TextBlock)window.FindName("VersionText");
      var scale = (Slider)window.FindName("ScaleSlider");
      var showLabels = (CheckBox)window.FindName("ShowLabels");
      Check(versionText.Text == expectedVersion, $"Visible version is '{versionText.Text}'.");
      Check(Math.Abs(scale.Value - 1.3) < 0.001, "Saved scale was not loaded into the window.");
      Check(showLabels.IsChecked == false, "Saved label setting was not loaded into the window.");
      Check(Math.Abs(settings.CubeScale - 1.3) < 0.001, "Opening the window changed the scale.");
      var after = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
      Check(before == null ? after == null : after != null && before.SequenceEqual(after),
        "Opening the window changed the user's settings file.");
      window.Close();
      Console.WriteLine($"PASS: Release settings window, version {assemblyVersion}, settings loaded without writing.");
      return 0;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine(ex);
      return 1;
    }
  }

  private static void Check(bool condition, string message)
  {
    if (!condition) throw new InvalidOperationException(message);
  }
}
