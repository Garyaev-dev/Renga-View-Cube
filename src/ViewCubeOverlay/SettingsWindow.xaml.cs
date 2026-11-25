using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace ViewCubeOverlay
{
  public partial class SettingsWindow : Window
  {
    private readonly ViewCubeSettings m_settings;
    private bool _updating = true;

    public event EventHandler SettingsChanged;
    public event EventHandler ScaleChanged;

    public SettingsWindow(ViewCubeSettings settings)
    {
      m_settings = settings ?? new ViewCubeSettings();
      InitializeComponent();
      VersionText.Text = typeof(SettingsWindow).Assembly.GetName().Version?.ToString() ?? "—";
      LoadFromSettings();
      WireColorFields();
    }

    public void LoadFromSettings()
    {
      _updating = true;
      try
      {
        FaceTop.Color = m_settings.FaceTop;
        FaceBottom.Color = m_settings.FaceBottom;
        FaceFront.Color = m_settings.FaceFront;
        FaceBack.Color = m_settings.FaceBack;
        FaceLeft.Color = m_settings.FaceLeft;
        FaceRight.Color = m_settings.FaceRight;
        LabelText.Color = m_settings.LabelText;
        LabelPill.Color = m_settings.LabelPill;
        ShowLabels.IsChecked = m_settings.ShowLabels;
        HotspotColor.Color = m_settings.HotspotColor;
        HoverColor.Color = m_settings.HoverColor;
        ActiveFaceColor.Color = m_settings.ActiveFaceColor;
        CompactChrome.IsChecked = m_settings.CompactChrome;
        ScaleSlider.Value = m_settings.CubeScale;
        ScaleValue.Text = m_settings.CubeScale.ToString("0.00");
        HighlightPreset(m_settings.StylePreset);
      }
      finally
      {
        _updating = false;
      }
    }

    private void WireColorFields()
    {
      foreach (var field in new[]
      {
        FaceTop, FaceBottom, FaceFront, FaceBack, FaceLeft, FaceRight,
        LabelText, LabelPill, HotspotColor, HoverColor, ActiveFaceColor
      })
        field.ColorChanged += ColorField_Changed;
    }

    private void PresetClassic_Click(object sender, RoutedEventArgs e)
    {
      ApplyPreset(ViewCubeSettings.PresetClassic);
    }

    private void PresetNeutral_Click(object sender, RoutedEventArgs e)
    {
      ApplyPreset(ViewCubeSettings.PresetNeutral);
    }

    private void PresetContrast_Click(object sender, RoutedEventArgs e)
    {
      ApplyPreset(ViewCubeSettings.PresetContrast);
    }

    private void PresetRenga_Click(object sender, RoutedEventArgs e)
    {
      ApplyPreset(ViewCubeSettings.PresetRenga);
    }

    private void ApplyPreset(string preset)
    {
      m_settings.ApplyPreset(preset);
      LoadFromSettings();
      RaiseChanged();
    }

    private void ColorField_Changed(object sender, EventArgs e)
    {
      if (_updating)
        return;
      CopyColorsToSettings();
      m_settings.StylePreset = ViewCubeSettings.PresetCustom;
      HighlightPreset(m_settings.StylePreset);
      RaiseChanged();
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
      if (_updating)
        return;
      m_settings.ShowLabels = ShowLabels.IsChecked == true;
      m_settings.CompactChrome = CompactChrome.IsChecked == true;
      RaiseChanged();
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
      if (_updating)
        return;
      m_settings.CubeScale = ScaleSlider.Value;
      ScaleValue.Text = m_settings.CubeScale.ToString("0.00");
      m_settings.Save();
      ScaleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Repository_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
      try
      {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
      }
      catch (Exception)
      {
        MessageBox.Show(this, "Не удалось открыть ссылку. Адрес репозитория: " + e.Uri.AbsoluteUri,
                        "Видовой куб", MessageBoxButton.OK, MessageBoxImage.Information);
      }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
      Close();
    }

    private void CopyColorsToSettings()
    {
      m_settings.FaceTop = FaceTop.Color;
      m_settings.FaceBottom = FaceBottom.Color;
      m_settings.FaceFront = FaceFront.Color;
      m_settings.FaceBack = FaceBack.Color;
      m_settings.FaceLeft = FaceLeft.Color;
      m_settings.FaceRight = FaceRight.Color;
      m_settings.LabelText = LabelText.Color;
      m_settings.LabelPill = LabelPill.Color;
      m_settings.HotspotColor = HotspotColor.Color;
      m_settings.HoverColor = HoverColor.Color;
      m_settings.ActiveFaceColor = ActiveFaceColor.Color;
    }

    private void HighlightPreset(string preset)
    {
      PresetClassic.FontWeight = preset == ViewCubeSettings.PresetClassic ? FontWeights.Bold : FontWeights.Normal;
      PresetNeutral.FontWeight = preset == ViewCubeSettings.PresetNeutral ? FontWeights.Bold : FontWeights.Normal;
      PresetContrast.FontWeight = preset == ViewCubeSettings.PresetContrast ? FontWeights.Bold : FontWeights.Normal;
      PresetRenga.FontWeight = preset == ViewCubeSettings.PresetRenga ? FontWeights.Bold : FontWeights.Normal;
    }

    private void RaiseChanged()
    {
      m_settings.Save();
      SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
  }
}
