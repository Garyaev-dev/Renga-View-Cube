using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ViewCubeOverlay
{
  public partial class ViewCubeWindow : Window
  {
    private readonly Renga.IApplication m_app;
    private ViewCubeSettings m_settings;
    private IntPtr m_rengaMainHwnd = IntPtr.Zero;

    private System.Windows.Threading.DispatcherTimer _timer;

    private Renga.FloatPoint3D? _lastCamPosition;
    private Renga.FloatPoint3D? _lastCamFocus;
    private Renga.FloatVector3D? _lastCamUp;

    private bool _isAnimating;
    private DateTime _animStart;
    private readonly TimeSpan _animDuration = TimeSpan.FromMilliseconds(280);
    private Renga.FloatPoint3D _animStartPos;
    private Renga.FloatPoint3D _animTargetPos;
    private Renga.FloatPoint3D _animFocus;
    private Renga.FloatVector3D _animStartUp;
    private Renga.FloatVector3D _animTargetUp;
    private double _animDistance;

    private bool _userMovedWindow;
    private bool _isDragging;
    private double _dockOffsetX;
    private double _dockOffsetY;
    private int _tickCounter;
    private SettingsWindow _settingsUi;

    public event EventHandler DockChanged;
    public event EventHandler AppearanceChanged;

    public ViewCubeWindow(Renga.IApplication app, ViewCubeSettings settings)
    {
      m_app = app;
      m_settings = settings ?? new ViewCubeSettings();
      InitializeComponent();

      ApplySettings(m_settings);

      CubeControl.FaceClicked += CubeControl_FaceClicked;
      CubeControl.CornerClicked += CubeControl_CornerClicked;
      CubeControl.EdgeClicked += CubeControl_EdgeClicked;
      CubeControl.ViewRotated += CubeControl_ViewRotated;
      CubeControl.HomeClicked += CubeControl_HomeClicked;
      CubeControl.ScaleStepRequested += CubeControl_ScaleStepRequested;

      _timer = new System.Windows.Threading.DispatcherTimer
      {
        Interval = TimeSpan.FromMilliseconds(33)
      };
      _timer.Tick += Timer_Tick;
    }

    public void ApplySettings(ViewCubeSettings settings)
    {
      m_settings = settings ?? new ViewCubeSettings();
      _userMovedWindow = m_settings.UserMovedWindow;
      _dockOffsetX = m_settings.DockOffsetX;
      _dockOffsetY = m_settings.DockOffsetY;
      ApplyAppearance(persist: false, reposition: true, rebuildCube: true);
    }

    public void WriteDockTo(ViewCubeSettings settings)
    {
      if (settings == null)
        return;

      settings.UserMovedWindow = _userMovedWindow;
      settings.DockOffsetX = _dockOffsetX;
      settings.DockOffsetY = _dockOffsetY;
    }

    public void SetRengaMainWindowHandle(int hwnd)
    {
      m_rengaMainHwnd = (IntPtr)hwnd;
      AttachOwner();
    }

    public void StartSync()
    {
      if (_timer != null && !_timer.IsEnabled)
        _timer.Start();
    }

    public void StopSync()
    {
      _isAnimating = false;
      _timer?.Stop();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
      base.OnSourceInitialized(e);
      // Не ставить RenderMode.SoftwareOnly: layered software поверх OpenGL/D3D
      // Renga заставляет DWM композитить кадр на CPU и убивает FPS.
      // Белый квад Viewport3D в AllowsTransparency прячется только на время DragMove.
      AttachOwner();
    }

    protected override void OnContentRendered(EventArgs e)
    {
      base.OnContentRendered(e);
      if (m_rengaMainHwnd != IntPtr.Zero)
        PositionOverRenga(force: true);
      StartSync();
    }

    protected override void OnClosed(EventArgs e)
    {
      CloseSettingsUi();
      WriteDockTo(m_settings);
      m_settings?.Save();
      StopSync();
      _timer = null;
      base.OnClosed(e);
    }

    private void AttachOwner()
    {
      if (m_rengaMainHwnd == IntPtr.Zero || !IsInitialized)
        return;

      try
      {
        var helper = new WindowInteropHelper(this)
        {
          Owner = m_rengaMainHwnd
        };
      }
      catch
      {
        // HWND мог стать недействительным при закрытии.
      }
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
      SyncFromRengaCamera();

      if (_isDragging)
        return;

      _tickCounter++;
      if (_tickCounter % 3 == 0 && IsVisible)
        PositionOverRenga(force: false);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
      {
        _isDragging = true;
        CubeControl.SetViewportHidden(true);
        try
        {
          DragMove();
          RememberDockOffset();
          _userMovedWindow = true;
          WriteDockTo(m_settings);
          m_settings.Save();
          DockChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
          // DragMove может бросить, если кнопка уже отпущена.
        }
        finally
        {
          _isDragging = false;
          CubeControl.SetViewportHidden(false);
        }

        e.Handled = true;
      }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
      Close();
    }

    private void ResetDockButton_Click(object sender, RoutedEventArgs e)
    {
      _userMovedWindow = false;
      _dockOffsetX = 0;
      _dockOffsetY = 0;
      m_settings?.ResetDock();
      PositionOverRenga(force: true);
      DockChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
      OpenSettings();
    }

    private void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
      OpenSettings();
    }

    private void ScaleUpMenu_Click(object sender, RoutedEventArgs e)
    {
      ChangeScaleBySteps(1);
    }

    private void ScaleDownMenu_Click(object sender, RoutedEventArgs e)
    {
      ChangeScaleBySteps(-1);
    }

    private void PresetClassicMenu_Click(object sender, RoutedEventArgs e)
    {
      ApplyPreset(ViewCubeSettings.PresetClassic);
    }

    private void PresetNeutralMenu_Click(object sender, RoutedEventArgs e)
    {
      ApplyPreset(ViewCubeSettings.PresetNeutral);
    }

    private void PresetContrastMenu_Click(object sender, RoutedEventArgs e)
    {
      ApplyPreset(ViewCubeSettings.PresetContrast);
    }

    private void PresetRengaMenu_Click(object sender, RoutedEventArgs e)
    {
      ApplyPreset(ViewCubeSettings.PresetRenga);
    }

    private void ShowLabelsMenu_Click(object sender, RoutedEventArgs e)
    {
      if (m_settings == null)
        return;
      m_settings.ShowLabels = ShowLabelsMenu.IsChecked;
      ApplyAppearance(persist: true, reposition: false, rebuildCube: true);
    }

    private void CompactChromeMenu_Click(object sender, RoutedEventArgs e)
    {
      if (m_settings == null)
        return;
      m_settings.CompactChrome = CompactChromeMenu.IsChecked;
      ApplyAppearance(persist: true, reposition: true, rebuildCube: false);
    }

    private void CubeControl_ScaleStepRequested(object sender, int steps)
    {
      ChangeScaleBySteps(steps);
    }

    private void ChangeScaleBySteps(int steps)
    {
      if (m_settings == null || !m_settings.ChangeScaleBySteps(steps))
        return;
      ApplyAppearance(persist: true, reposition: true, rebuildCube: false);
    }

    private void ApplyPreset(string preset)
    {
      if (m_settings == null)
        return;
      m_settings.ApplyPreset(preset);
      ApplyAppearance(persist: true, reposition: false, rebuildCube: true);
      _settingsUi?.LoadFromSettings();
    }

    private void ApplyAppearance(bool persist, bool reposition, bool rebuildCube)
    {
      if (m_settings == null)
        return;

      Width = m_settings.WindowWidth;
      Height = m_settings.WindowHeight;
      ApplyChrome();
      if (rebuildCube)
        CubeControl.ApplyAppearance(m_settings);
      ShowLabelsMenu.IsChecked = m_settings.ShowLabels;
      CompactChromeMenu.IsChecked = m_settings.CompactChrome;
      if (_settingsUi != null && _settingsUi.IsLoaded && !_settingsUi.IsActive)
        _settingsUi.LoadFromSettings();

      if (reposition && IsVisible)
        PositionOverRenga(force: true);

      if (persist)
      {
        WriteDockTo(m_settings);
        m_settings.Save();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
      }
    }

    private void ApplyChrome()
    {
      bool compact = m_settings != null && m_settings.CompactChrome;
      double scale = m_settings != null ? m_settings.CubeScale : 1.0;
      double titleH = compact ? Math.Max(16, 18 * scale) : Math.Max(18, 24 * Math.Min(1.0, scale));
      TitleBar.Height = titleH;
      TitleBar.Background = compact
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x70, 0, 0, 0))
        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x90, 0, 0, 0));
      TitleText.Visibility = compact || Width < 168 ? Visibility.Collapsed : Visibility.Visible;
      TitleText.FontSize = compact ? 10 : 11;
      CubeControl.Margin = new Thickness(0, titleH, 0, 0);
    }

    private void OpenSettings()
    {
      if (m_settings == null)
        return;

      if (_settingsUi != null && _settingsUi.IsLoaded)
      {
        _settingsUi.LoadFromSettings();
        _settingsUi.Activate();
        return;
      }

      _settingsUi = new SettingsWindow(m_settings)
      {
        Owner = this
      };
      _settingsUi.SettingsChanged += SettingsUi_SettingsChanged;
      _settingsUi.ScaleChanged += SettingsUi_ScaleChanged;
      _settingsUi.Closed += SettingsUi_Closed;
      _settingsUi.Show();
    }

    private void SettingsUi_SettingsChanged(object sender, EventArgs e)
    {
      ApplyAppearance(persist: true, reposition: true, rebuildCube: true);
    }

    private void SettingsUi_ScaleChanged(object sender, EventArgs e)
    {
      ApplyAppearance(persist: true, reposition: true, rebuildCube: false);
    }

    private void SettingsUi_Closed(object sender, EventArgs e)
    {
      if (_settingsUi != null)
      {
        _settingsUi.SettingsChanged -= SettingsUi_SettingsChanged;
        _settingsUi.ScaleChanged -= SettingsUi_ScaleChanged;
        _settingsUi.Closed -= SettingsUi_Closed;
      }
      _settingsUi = null;
    }

    private void CloseSettingsUi()
    {
      if (_settingsUi == null)
        return;
      _settingsUi.SettingsChanged -= SettingsUi_SettingsChanged;
      _settingsUi.ScaleChanged -= SettingsUi_ScaleChanged;
      _settingsUi.Closed -= SettingsUi_Closed;
      try
      {
        if (_settingsUi.IsLoaded)
          _settingsUi.Close();
      }
      catch
      {
      }
      _settingsUi = null;
    }

    private void RememberDockOffset()
    {
      if (m_rengaMainHwnd == IntPtr.Zero)
        return;
      if (!GetWindowRect(m_rengaMainHwnd, out var rect))
        return;

      GetDpiScale(out double dpiX, out double dpiY);
      double dockLeft = (rect.Right * dpiX) - Width - 16;
      double dockTop = (rect.Bottom * dpiY) - Height - 16;
      _dockOffsetX = Left - dockLeft;
      _dockOffsetY = Top - dockTop;
    }

    private void PositionOverRenga(bool force)
    {
      if (_isDragging)
        return;
      if (m_rengaMainHwnd == IntPtr.Zero)
        return;
      if (!GetWindowRect(m_rengaMainHwnd, out var rect))
        return;

      GetDpiScale(out double dpiX, out double dpiY);

      const int margin = 16;
      double left = (rect.Right * dpiX) - Width - margin;
      double top = (rect.Bottom * dpiY) - Height - margin;

      if (_userMovedWindow)
      {
        left += _dockOffsetX;
        top += _dockOffsetY;
      }

      if (force || Math.Abs(Left - left) > 1.0 || Math.Abs(Top - top) > 1.0)
      {
        Left = left;
        Top = top;
      }
    }

    private void GetDpiScale(out double dpiX, out double dpiY)
    {
      dpiX = 1.0;
      dpiY = 1.0;
      var source = PresentationSource.FromVisual(this);
      if (source?.CompositionTarget != null)
      {
        dpiX = source.CompositionTarget.TransformFromDevice.M11;
        dpiY = source.CompositionTarget.TransformFromDevice.M22;
      }
    }

    private void SyncFromRengaCamera()
    {
      if (_isAnimating)
      {
        if (!TryGetCamera(out var animCam))
        {
          _isAnimating = false;
          return;
        }

        var elapsed = DateTime.Now - _animStart;
        double rawT = _animDuration.TotalMilliseconds <= 0
          ? 1.0
          : Math.Min(1.0, Math.Max(0.0, elapsed.TotalMilliseconds / _animDuration.TotalMilliseconds));
        double t = CameraMath.SmoothStep(rawT);

        var startDir = new Vector3D(
          _animFocus.X - _animStartPos.X,
          _animFocus.Y - _animStartPos.Y,
          _animFocus.Z - _animStartPos.Z);
        var targetDir = new Vector3D(
          _animFocus.X - _animTargetPos.X,
          _animFocus.Y - _animTargetPos.Y,
          _animFocus.Z - _animTargetPos.Z);

        Renga.FloatPoint3D pos;
        if (startDir.LengthSquared > 1e-6 && targetDir.LengthSquared > 1e-6)
        {
          startDir.Normalize();
          targetDir.Normalize();
          var interpDir = CameraMath.Slerp(startDir, targetDir, t);
          pos = new Renga.FloatPoint3D
          {
            X = (float)(_animFocus.X - interpDir.X * _animDistance),
            Y = (float)(_animFocus.Y - interpDir.Y * _animDistance),
            Z = (float)(_animFocus.Z - interpDir.Z * _animDistance)
          };
        }
        else
        {
          pos = CameraMath.Lerp(_animStartPos, _animTargetPos, t);
        }

        var up = CameraMath.Lerp(_animStartUp, _animTargetUp, t);
        var focus = _animFocus;

        animCam.LookAt(focus, pos, up);

        var dirAnim = new Vector3D(focus.X - pos.X, focus.Y - pos.Y, focus.Z - pos.Z);
        if (dirAnim.LengthSquared > 1e-6)
        {
          dirAnim.Normalize();
          CubeControl.UpdateCameraSync(
            new Vector3D(dirAnim.X, dirAnim.Y, dirAnim.Z),
            new Vector3D(up.X, up.Y, up.Z));
        }

        _lastCamPosition = pos;
        _lastCamFocus = focus;
        _lastCamUp = up;

        if (rawT >= 1.0)
          _isAnimating = false;

        return;
      }

      if (!TryGetCamera(out var cam))
        return;

      var posCurrent = cam.Position;
      var focusCurrent = cam.FocusPoint;
      var upCurrent = cam.UpVector;

      if (_lastCamPosition.HasValue &&
          _lastCamFocus.HasValue &&
          _lastCamUp.HasValue &&
          IsSameCameraState(posCurrent, focusCurrent, upCurrent,
                            _lastCamPosition.Value,
                            _lastCamFocus.Value,
                            _lastCamUp.Value))
      {
        return;
      }

      _lastCamPosition = posCurrent;
      _lastCamFocus = focusCurrent;
      _lastCamUp = upCurrent;

      var dir = new Vector3D(focusCurrent.X - posCurrent.X, focusCurrent.Y - posCurrent.Y, focusCurrent.Z - posCurrent.Z);
      if (dir.LengthSquared < 1e-6)
        return;
      dir.Normalize();

      CubeControl.UpdateCameraSync(
        new Vector3D(dir.X, dir.Y, dir.Z),
        new Vector3D(upCurrent.X, upCurrent.Y, upCurrent.Z));
    }

    private static bool IsSameCameraState(
      Renga.FloatPoint3D pos, Renga.FloatPoint3D focus, Renga.FloatVector3D up,
      Renga.FloatPoint3D lastPos, Renga.FloatPoint3D lastFocus, Renga.FloatVector3D lastUp)
    {
      const double posEpsSq = 1e-4;
      const double focusEpsSq = 1e-4;
      const double upEpsSq = 1e-6;

      return CameraMath.DistanceSquared(pos, lastPos) < posEpsSq &&
             CameraMath.DistanceSquared(focus, lastFocus) < focusEpsSq &&
             CameraMath.DistanceSquared(up, lastUp) < upEpsSq;
    }

    private void CubeControl_FaceClicked(object sender, ViewCubeControl.CubeFace face)
    {
      ApplyFaceView(face);
    }

    private void CubeControl_CornerClicked(object sender, ViewCubeControl.CubeCorner corner)
    {
      ApplyCornerView(corner);
    }

    private void CubeControl_EdgeClicked(object sender, ViewCubeControl.CubeEdge edge)
    {
      ApplyEdgeView(edge);
    }

    private void CubeControl_ViewRotated(object sender, ViewCubeControl.ViewRotationEventArgs e)
    {
      if (!TryGetCamera(out var cam))
        return;

      var dir = e.Direction;
      var focus = cam.FocusPoint;
      double d = Math.Max(1000.0, CameraMath.Distance(cam.Position, focus));

      var pos = new Renga.FloatPoint3D
      {
        X = (float)(focus.X - dir.X * d),
        Y = (float)(focus.Y - dir.Y * d),
        Z = (float)(focus.Z - dir.Z * d)
      };

      var up = CameraMath.Vec(0, 0, 1);
      cam.LookAt(focus, pos, up);

      _lastCamPosition = pos;
      _lastCamFocus = focus;
      _lastCamUp = up;

      CubeControl.UpdateCameraSync(dir, new Vector3D(0, 0, 1));
    }

    private void CubeControl_HomeClicked(object sender, EventArgs e)
    {
      ApplyCornerView(ViewCubeControl.CubeCorner.TopFrontRight);
    }

    private void ApplyFaceView(ViewCubeControl.CubeFace face)
    {
      if (!TryGetCamera(out var cam))
        return;

      var focus = cam.FocusPoint;
      double d = Math.Max(1000.0, CameraMath.Distance(cam.Position, focus));
      if (!CameraMath.TryGetFaceCamera(face, focus, d, out var pos, out var up))
        return;

      BeginCameraAnimation(cam.Position, pos, focus, cam.UpVector, up, d);
    }

    private void ApplyCornerView(ViewCubeControl.CubeCorner corner)
    {
      if (!CameraMath.TryGetCornerDirection(corner, out var dir))
        return;
      ApplyDirectionView(dir);
    }

    private void ApplyEdgeView(ViewCubeControl.CubeEdge edge)
    {
      if (!CameraMath.TryGetEdgeDirection(edge, out var dir))
        return;
      ApplyDirectionView(dir);
    }

    private void ApplyDirectionView(Vector3D dir)
    {
      if (!TryGetCamera(out var cam))
        return;

      var focus = cam.FocusPoint;
      double d = Math.Max(1000.0, CameraMath.Distance(cam.Position, focus));

      var pos = new Renga.FloatPoint3D
      {
        X = (float)(focus.X + dir.X * d),
        Y = (float)(focus.Y + dir.Y * d),
        Z = (float)(focus.Z + dir.Z * d)
      };

      var up = CameraMath.Vec(0, 0, 1);
      BeginCameraAnimation(cam.Position, pos, focus, cam.UpVector, up, d);
    }

    private void BeginCameraAnimation(
      Renga.FloatPoint3D startPos,
      Renga.FloatPoint3D targetPos,
      Renga.FloatPoint3D focus,
      Renga.FloatVector3D startUp,
      Renga.FloatVector3D targetUp,
      double distance)
    {
      _animStart = DateTime.Now;
      _animStartPos = startPos;
      _animTargetPos = targetPos;
      _animFocus = focus;
      _animStartUp = startUp;
      _animTargetUp = targetUp;
      _animDistance = distance;
      _isAnimating = true;
      StartSync();
    }

    private bool TryGetCamera(out Renga.ICamera3D camera)
    {
      camera = null;
      if (m_app.Project == null)
        return false;
      var view = m_app.ActiveView;
      if (view == null || view.Type != Renga.ViewType.ViewType_View3D)
        return false;
      var view3D = view as Renga.IView3DParams;
      if (view3D == null)
        return false;
      camera = view3D.Camera;
      return camera != null;
    }

    #region Win32
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
      public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    #endregion
  }
}
