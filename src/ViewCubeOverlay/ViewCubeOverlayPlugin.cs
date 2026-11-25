/*  ViewCube Overlay — плавающее окно с 3D-кубом поверх интерфейса Renga.
 *  По клику по граням/углам переключается вид камеры в 3D-виде.
 *
 *  Разработчик: Гаряев Иван Иванович
 */

using System.IO;

namespace ViewCubeOverlay
{
  public class ViewCubeOverlayPlugin : Renga.IPlugin
  {
    private Renga.IApplication m_app;
    private Renga.IAction m_action;
    private Renga.ApplicationEventSource m_appEvents;
    private readonly List<Renga.ActionEventSource> m_eventSources = new List<Renga.ActionEventSource>();
    private ViewCubeWindow m_window;
    private ViewCubeSettings m_settings;
    private bool m_userWantsWindow;
    private bool m_initialized;

    public bool Initialize(string pluginFolder)
    {
      try
      {
        m_app = new Renga.Application();
        m_settings = ViewCubeSettings.Load();
        m_userWantsWindow = m_settings.UserWantsWindow;

        var ui = m_app.UI;
        var panelExtension = ui.CreateUIPanelExtension();

        m_action = ui.CreateAction();
        m_action.DisplayName = "Видовой куб (окно)";
        m_action.ToolTip = "Показать видовой куб поверх 3D-вида";
        m_action.Checkable = true;
        m_action.Checked = false;

        TryLoadIcon(ui, pluginFolder, m_action);

        var eventSource = new Renga.ActionEventSource(m_action);
        eventSource.Triggered += OnActionTriggered;
        m_eventSources.Add(eventSource);
        panelExtension.AddToolButton(m_action);

        ui.AddExtensionToPrimaryPanel(panelExtension);
        ui.AddExtensionToActionsPanel(panelExtension, Renga.ViewType.ViewType_View3D);

        m_appEvents = new Renga.ApplicationEventSource(m_app);
        m_appEvents.ProjectClosed += OnProjectClosed;
        m_appEvents.BeforeApplicationClose += OnBeforeApplicationClose;
        try
        {
          m_appEvents.ActiveViewChanged += OnActiveViewChanged;
        }
        catch (Exception ex)
        {
          LogError("ActiveViewChanged subscribe failed", ex);
        }

        m_initialized = true;
        UpdateActionEnabled();
        return true;
      }
      catch (Exception ex)
      {
        LogError("Initialize failed", ex);
        return false;
      }
    }

    public void Stop()
    {
      try
      {
        PersistSettings();
        CloseWindow(persistWant: false);
        m_userWantsWindow = false;
        m_initialized = false;

        if (m_action != null)
        {
          m_action.Checked = false;
          m_action.Enabled = false;
        }

        foreach (var ev in m_eventSources)
          ev.Dispose();
        m_eventSources.Clear();

        m_appEvents?.Dispose();
        m_appEvents = null;
      }
      catch (Exception ex)
      {
        LogError("Stop failed", ex);
      }
    }

    private void OnActionTriggered(object sender, EventArgs e)
    {
      try
      {
        if (m_action.Checked)
        {
          m_userWantsWindow = true;
          PersistSettings();
          ShowWindow(activate: true);
        }
        else
        {
          m_userWantsWindow = false;
          PersistSettings();
          CloseWindow(persistWant: true);
        }
      }
      catch (Exception ex)
      {
        LogError("OnActionTriggered failed", ex);
      }
    }

    private void OnActiveViewChanged(int activatedViewId)
    {
      if (!m_initialized)
        return;

      try
      {
        UpdateActionEnabled();
        UpdateWindowForActiveView();
      }
      catch (Exception ex)
      {
        LogError("OnActiveViewChanged failed", ex);
      }
    }

    private void OnProjectClosed()
    {
      try
      {
        CloseWindow(persistWant: true);
        if (m_action != null)
        {
          m_action.Checked = false;
          m_action.Enabled = false;
        }
      }
      catch (Exception ex)
      {
        LogError("OnProjectClosed failed", ex);
      }
    }

    private void OnBeforeApplicationClose(Renga.ApplicationCloseEventArgs args)
    {
      try
      {
        PersistSettings();
        CloseWindow(persistWant: false);
      }
      catch (Exception ex)
      {
        LogError("OnBeforeApplicationClose failed", ex);
      }
    }

    private void UpdateActionEnabled()
    {
      if (m_action == null)
        return;

      bool enabled = IsActiveView3D();
      m_action.Enabled = enabled;
      if (!enabled)
        m_action.Checked = false;
      else if (m_userWantsWindow)
        m_action.Checked = true;
    }

    private void UpdateWindowForActiveView()
    {
      if (!m_userWantsWindow)
        return;

      if (IsActiveView3D())
      {
        ShowWindow(activate: false);
      }
      else if (m_window != null)
      {
        m_window.StopSync();
        if (m_window.IsVisible)
          m_window.Hide();
        PersistWindowDock();
      }
    }

    private void ShowWindow(bool activate)
    {
      if (m_window == null || !m_window.IsLoaded)
      {
        m_window = new ViewCubeWindow(m_app, m_settings);
        m_window.Closed += OnWindowClosed;
        m_window.DockChanged += OnDockChanged;
        m_window.AppearanceChanged += OnDockChanged;
      }

      m_window.SetRengaMainWindowHandle(m_app.GetMainWindowHandle());
      m_window.ApplySettings(m_settings);

      if (IsActiveView3D())
      {
        m_window.Show();
        m_window.StartSync();
        if (activate)
          m_window.Activate();
      }
      else
      {
        m_window.StopSync();
        if (m_window.IsVisible)
          m_window.Hide();
      }

      if (m_action != null && IsActiveView3D())
        m_action.Checked = true;
    }

    private void CloseWindow(bool persistWant)
    {
      if (m_window == null)
        return;

      PersistWindowDock();
      m_window.Closed -= OnWindowClosed;
      m_window.DockChanged -= OnDockChanged;
      m_window.AppearanceChanged -= OnDockChanged;
      m_window.StopSync();
      if (m_window.IsLoaded)
        m_window.Close();
      m_window = null;

      if (persistWant)
        PersistSettings();
    }

    private void OnWindowClosed(object sender, EventArgs e)
    {
      if (ReferenceEquals(sender, m_window))
      {
        PersistWindowDock();
        m_window = null;
      }

      m_userWantsWindow = false;
      PersistSettings();
      if (m_action != null)
        m_action.Checked = false;
    }

    private void OnDockChanged(object sender, EventArgs e)
    {
      PersistWindowDock();
    }

    private void PersistWindowDock()
    {
      if (m_window == null || m_settings == null)
        return;

      PersistSettings();
    }

    private void PersistSettings()
    {
      if (m_settings == null)
        return;

      if (m_window != null)
        m_window.WriteDockTo(m_settings);

      m_settings.UserWantsWindow = m_userWantsWindow;
      m_settings.Save();
    }

    private bool IsActiveView3D()
    {
      try
      {
        var view = m_app?.ActiveView;
        return view != null && view.Type == Renga.ViewType.ViewType_View3D;
      }
      catch
      {
        return false;
      }
    }

    private static void TryLoadIcon(Renga.IUI ui, string pluginFolder, Renga.IAction action)
    {
      try
      {
        var iconPath = Path.Combine(pluginFolder, "icon.png");
        if (!File.Exists(iconPath) || action == null)
          return;

        var image = ui.CreateImage();
        image.LoadFromFile(iconPath);
        action.Icon = image;
      }
      catch
      {
      }
    }

    private static void LogError(string message, Exception ex)
    {
      try
      {
        var dir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
          "ViewCubeOverlay");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "error.log");
        var text = DateTime.Now.ToString("s") + " " + message + Environment.NewLine
                   + ex + Environment.NewLine + Environment.NewLine;
        File.AppendAllText(path, text);
      }
      catch
      {
      }
    }
  }
}
