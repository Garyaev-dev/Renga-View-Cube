using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ViewCubeOverlay
{
  public partial class ColorField : UserControl
  {
    private Color _color = Colors.White;
    private bool _updating;
    private bool _ignoreDeactivate;
    private Window _hostWindow;
    private readonly SolidColorBrush _swatchBrush = new SolidColorBrush(Colors.White);

    public event EventHandler ColorChanged;

    public ColorField()
    {
      InitializeComponent();
      Swatch.Background = _swatchBrush;
      SetColor(Colors.White, raise: false);
      Picker.Opened += Picker_Opened;
      Picker.Closed += Picker_Closed;
      Unloaded += ColorField_Unloaded;
    }

    public Color Color
    {
      get { return _color; }
      set { SetColor(value, raise: false); }
    }

    public void SetColor(Color color, bool raise)
    {
      _color = color;
      _updating = true;
      try
      {
        HexBox.Text = ViewCubeSettings.ColorToHex(color);
        _swatchBrush.Color = Color.FromRgb(color.R, color.G, color.B);
        SliderR.Value = color.R;
        SliderG.Value = color.G;
        SliderB.Value = color.B;
        SliderA.Value = color.A;
        RgbLabel.Text = string.Format(
          CultureInfo.InvariantCulture,
          "R {0}  G {1}  B {2}  A {3}",
          color.R, color.G, color.B, color.A);
      }
      finally
      {
        _updating = false;
      }

      if (raise)
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HexBox_LostFocus(object sender, RoutedEventArgs e)
    {
      CommitHex();
    }

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Enter)
      {
        CommitHex();
        e.Handled = true;
      }
    }

    private void CommitHex()
    {
      var parsed = ViewCubeSettings.ParseColor(HexBox.Text, _color);
      if (parsed != _color)
        SetColor(parsed, raise: true);
      else
        SetColor(_color, raise: false);
    }

    private void Swatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      Picker.IsOpen = !Picker.IsOpen;
      e.Handled = true;
    }

    private void PickerOk_Click(object sender, RoutedEventArgs e)
    {
      Picker.IsOpen = false;
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
      if (_updating)
        return;

      var next = Color.FromArgb(
        (byte)SliderA.Value,
        (byte)SliderR.Value,
        (byte)SliderG.Value,
        (byte)SliderB.Value);
      SetColor(next, raise: true);
    }

    private void Picker_Opened(object sender, EventArgs e)
    {
      _ignoreDeactivate = true;
      _hostWindow = Window.GetWindow(this);
      if (_hostWindow != null)
      {
        _hostWindow.PreviewMouseDown += Host_PreviewMouseDown;
        _hostWindow.PreviewKeyDown += Host_PreviewKeyDown;
        _hostWindow.Deactivated += Host_Deactivated;
      }

      Dispatcher.BeginInvoke(new Action(() =>
      {
        _ignoreDeactivate = false;
        PickerPanel.Focus();
      }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Picker_Closed(object sender, EventArgs e)
    {
      UnhookHost();
    }

    private void ColorField_Unloaded(object sender, RoutedEventArgs e)
    {
      Picker.IsOpen = false;
      UnhookHost();
    }

    private void UnhookHost()
    {
      if (_hostWindow == null)
        return;

      _hostWindow.PreviewMouseDown -= Host_PreviewMouseDown;
      _hostWindow.PreviewKeyDown -= Host_PreviewKeyDown;
      _hostWindow.Deactivated -= Host_Deactivated;
      _hostWindow = null;
    }

    private void Host_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
      if (!Picker.IsOpen)
        return;

      // Клики по слайдерам идут в HWND попапа и сюда не доходят.
      // Клик по образцу обрабатывает переключатель на Swatch.
      if (Swatch.IsMouseOver)
        return;

      Picker.IsOpen = false;
    }

    private void Host_PreviewKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Escape && Picker.IsOpen)
      {
        Picker.IsOpen = false;
        e.Handled = true;
      }
    }

    private void Host_Deactivated(object sender, EventArgs e)
    {
      if (_ignoreDeactivate)
        return;

      Picker.IsOpen = false;
    }
  }
}
