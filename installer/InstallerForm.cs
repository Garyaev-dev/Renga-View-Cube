using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ViewCubeInstaller
{
  internal sealed class InstallerForm : Form
  {
    private readonly ComboBox locations = new ComboBox();
    private readonly Label state = new Label();
    private readonly Label destination = new Label();
    private readonly Button install = new Button();
    private readonly Label note = new Label();
    private bool finished;

    internal InstallerForm()
    {
      Text = "Установка видового куба";
      StartPosition = FormStartPosition.CenterScreen;
      MinimumSize = new Size(620, 470);
      Size = new Size(660, 500);
      Font = new Font("Segoe UI", 10F);
      BackColor = Color.FromArgb(246, 248, 251);
      AutoScaleMode = AutoScaleMode.Dpi;

      var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 20), ColumnCount = 1, RowCount = 3 };
      layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 103));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
      layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
      Controls.Add(layout);

      var header = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
      header.Controls.Add(new Label { Text = "Видовой куб для Renga", AutoSize = true, Font = new Font("Segoe UI", 19F, FontStyle.Bold), ForeColor = Color.FromArgb(32, 48, 67), Margin = new Padding(0, 0, 0, 4) });
      header.Controls.Add(new Label { Text = "Установка плагина  •  версия " + Program.PluginVersion(), AutoSize = true, ForeColor = Color.FromArgb(97, 112, 131), Margin = new Padding(0) });
      layout.Controls.Add(header, 0, 0);

      var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 18, 20, 16) };
      layout.Controls.Add(body, 0, 1);
      var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7 };
      fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
      fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
      fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
      fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
      fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
      fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
      fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
      fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
      body.Controls.Add(fields);
      var folderTitle = new Label { Text = "Папка установленной Renga", Font = new Font("Segoe UI", 10F, FontStyle.Bold), Dock = DockStyle.Fill, AutoSize = false };
      fields.Controls.Add(folderTitle, 0, 0); fields.SetColumnSpan(folderTitle, 2);
      locations.Dock = DockStyle.Fill; locations.DropDownStyle = ComboBoxStyle.DropDownList; locations.Margin = new Padding(0, 0, 9, 0); locations.SelectedIndexChanged += SelectedInstallationChanged;
      fields.Controls.Add(locations, 0, 1);
      var browse = new Button { Text = "Обзор…", Dock = DockStyle.Fill, Margin = new Padding(0), Cursor = Cursors.Hand }; browse.Click += Browse;
      fields.Controls.Add(browse, 1, 1);
      state.Dock = DockStyle.Fill; state.ForeColor = Color.FromArgb(42, 116, 73); state.Padding = new Padding(0, 7, 0, 0);
      fields.Controls.Add(state, 0, 2); fields.SetColumnSpan(state, 2);
      var targetTitle = new Label { Text = "Папка плагина", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(87, 101, 119) };
      fields.Controls.Add(targetTitle, 0, 3); fields.SetColumnSpan(targetTitle, 2);
      destination.Dock = DockStyle.Fill; destination.ForeColor = Color.FromArgb(52, 67, 85); destination.AutoEllipsis = true;
      fields.Controls.Add(destination, 0, 4); fields.SetColumnSpan(destination, 2);
      note.Text = "Перед установкой закройте Renga. Ваши настройки куба сохранятся."; note.Dock = DockStyle.Fill; note.ForeColor = Color.FromArgb(104, 119, 137); note.AutoEllipsis = true;
      fields.Controls.Add(note, 0, 6); fields.SetColumnSpan(note, 2);

      var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 16, 0, 0), WrapContents = false };
      var cancel = new Button { Text = "Отмена", Width = 110, Height = 34, Margin = new Padding(8, 0, 0, 0) }; cancel.Click += delegate { Close(); }; footer.Controls.Add(cancel);
      install.Text = "Установить"; install.Width = 130; install.Height = 34; install.BackColor = Color.FromArgb(36, 102, 188); install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat; install.FlatAppearance.BorderSize = 0; install.Margin = new Padding(0); install.Cursor = Cursors.Hand; install.Click += InstallClicked; footer.Controls.Add(install);
      layout.Controls.Add(footer, 0, 2); AcceptButton = install; CancelButton = cancel;

      var found = RengaDiscovery.FindInstallations();
      foreach (string path in found) locations.Items.Add(path);
      if (found.Count == 1) locations.SelectedIndex = 0;
      else RefreshState();
    }

    private void SelectedInstallationChanged(object sender, EventArgs e) { RefreshState(); }
    private string SelectedPath { get { return locations.SelectedItem as string; } }
    private void RefreshState()
    {
      string path = SelectedPath;
      bool valid = RengaDiscovery.IsValid(path);
      install.Enabled = valid && !finished;
      state.ForeColor = valid ? Color.FromArgb(42, 116, 73) : Color.FromArgb(158, 96, 23);
      bool alreadyInstalled = valid && File.Exists(Path.Combine(path, "Plugins", "ViewCubeOverlay", "ViewCubeOverlay.rndesc"));
      install.Text = alreadyInstalled ? "Обновить" : "Установить";
      state.Text = valid ? (alreadyInstalled ? "Плагин найден. Можно обновить установленную версию." : "Renga найдена. Можно установить плагин.") : (locations.Items.Count > 1 ? "Найдено несколько установок. Выберите нужную." : "Renga не найдена автоматически. Укажите папку через «Обзор».");
      destination.Text = valid ? Path.Combine(path, "Plugins", "ViewCubeOverlay") : "Выберите папку, содержащую Renga.exe";
    }

    private void Browse(object sender, EventArgs e)
    {
      using (var dialog = new FolderBrowserDialog())
      {
        dialog.Description = "Выберите папку установленной Renga, в которой находится Renga.exe.";
        dialog.ShowNewFolderButton = false;
        if (RengaDiscovery.IsValid(SelectedPath)) dialog.SelectedPath = SelectedPath;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!RengaDiscovery.IsValid(dialog.SelectedPath))
        {
          MessageBox.Show(this, "В выбранной папке не найден Renga.exe. Выберите папку программы Renga.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }
        string selected = Path.GetFullPath(dialog.SelectedPath).TrimEnd(Path.DirectorySeparatorChar);
        int index = -1;
        for (int i = 0; i < locations.Items.Count; i++)
          if (String.Equals(locations.Items[i].ToString(), selected, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
        if (index < 0) index = locations.Items.Add(selected);
        locations.SelectedIndex = index;
      }
    }

    private void InstallClicked(object sender, EventArgs e)
    {
      string path = SelectedPath;
      if (!RengaDiscovery.IsValid(path)) { RefreshState(); return; }
      var running = Process.GetProcessesByName("Renga");
      bool active = running.Length > 0;
      foreach (var process in running) process.Dispose();
      if (active)
      {
        MessageBox.Show(this, "Закройте Renga и нажмите «Установить» снова.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }
      install.Enabled = false; UseWaitCursor = true;
      try
      {
        Program.Install(path);
        Finished();
      }
      catch (UnauthorizedAccessException)
      {
        UseWaitCursor = false;
        var answer = MessageBox.Show(this, "Для записи в эту папку нужны права администратора. Продолжить с запросом Windows?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer == DialogResult.Yes) TryElevated(path);
      }
      catch (Exception ex) { MessageBox.Show(this, "Не удалось установить плагин.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
      finally { UseWaitCursor = false; RefreshState(); }
    }

    private void TryElevated(string path)
    {
      try
      {
        var start = new ProcessStartInfo(Application.ExecutablePath, "--elevated-install \"" + path + "\"") { UseShellExecute = true, Verb = "runas" };
        using (var child = Process.Start(start))
        {
          if (child == null) throw new InvalidOperationException("Не удалось запустить установку.");
          child.WaitForExit();
          if (child.ExitCode == 0) Finished();
          else if (child.ExitCode != 2) MessageBox.Show(this, "Установка не завершена. Проверьте сообщение об ошибке.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
      }
      catch (Win32Exception ex) { if (ex.NativeErrorCode != 1223) MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
      catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void Finished()
    {
      finished = true;
      state.Text = "Плагин установлен";
      note.Text = "Запустите Renga, откройте 3D-вид и нажмите «Видовой куб (окно)».";
      MessageBox.Show(this, "Плагин установлен. Запустите Renga и откройте 3D-вид.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
      Close();
    }
  }
}
