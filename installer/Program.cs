using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;

namespace ViewCubeInstaller
{
  internal static class Program
  {
    private static readonly string[] Files = {
      "ViewCubeOverlay.dll", "ViewCubeOverlay.deps.json", "ViewCubeOverlay.rndesc",
      "icon.png", "Renga.NET8.PluginUtility.dll", "Interop.Renga.dll"
    };

    [STAThread]
    private static int Main(string[] args)
    {
      if (args.Length == 1 && args[0] == "--self-test") return SelfTest();
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      if (args.Length == 2 && args[0] == "--elevated-install")
      {
        if (Process.GetProcessesByName("Renga").Length > 0) { MessageBox.Show("Закройте Renga перед установкой.", "Видовой куб", MessageBoxButtons.OK, MessageBoxIcon.Warning); return 2; }
        try { Install(args[1]); return 0; }
        catch (Exception ex) { MessageBox.Show("Не удалось установить плагин.\n\n" + ex.Message, "Видовой куб", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
      }
      if (args.Length != 0) return 2;
      using (var form = new InstallerForm())
        Application.Run(form);
      return 0;
    }

    internal static string PluginVersion()
    {
      try
      {
        using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("ViewCubeInstaller.payload.zip"))
        using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
        using (Stream manifest = archive.GetEntry("ViewCubeOverlay.rndesc").Open())
        {
          var document = new System.Xml.XmlDocument();
          document.Load(manifest);
          return document.SelectSingleNode("/RengaPlugin/Version").InnerText;
        }
      }
      catch (Exception) { return "неизвестна"; }
    }

    internal static void Install(string rengaPath)
    {
      if (!File.Exists(Path.Combine(rengaPath, "Renga.exe")))
        throw new InvalidOperationException("В выбранной папке не найден Renga.exe.");
      string target = Path.Combine(rengaPath, "Plugins", "ViewCubeOverlay");
      string staging = Path.Combine(Path.GetTempPath(), "ViewCubeInstaller-" + Guid.NewGuid().ToString("N"));
      string backup = Path.Combine(staging, "backup");
      var changed = new System.Collections.Generic.List<string>();
      bool keepBackup = false;
      try
      {
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(backup);
        using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("ViewCubeInstaller.payload.zip"))
        {
          if (payload == null) throw new InvalidOperationException("Не найден комплект файлов плагина.");
          using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
          {
            foreach (string name in Files)
            {
              ZipArchiveEntry entry = archive.GetEntry(name);
              if (entry == null) throw new InvalidOperationException("В комплекте отсутствует " + name);
              entry.ExtractToFile(Path.Combine(staging, name));
            }
          }
        }
        Directory.CreateDirectory(target);
        // Check write permission before replacing any existing files.
        string probe = Path.Combine(target, ".viewcube-write-" + Guid.NewGuid().ToString("N"));
        using (FileStream test = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        File.Delete(probe);
        foreach (string name in Files)
        {
          string destination = Path.Combine(target, name);
          if (File.Exists(destination)) File.Copy(destination, Path.Combine(backup, name));
        }
        try
        {
          foreach (string name in Files)
          {
            changed.Add(name);
            File.Copy(Path.Combine(staging, name), Path.Combine(target, name), true);
          }
        }
        catch (Exception installError)
        {
          var rollbackErrors = new System.Collections.Generic.List<string>();
          foreach (string name in changed)
          {
            try
            {
              string original = Path.Combine(backup, name);
              string destination = Path.Combine(target, name);
              if (File.Exists(original)) File.Copy(original, destination, true);
              else if (File.Exists(destination)) File.Delete(destination);
            }
            catch (Exception rollbackError) { rollbackErrors.Add(name + ": " + rollbackError.Message); }
          }
          if (rollbackErrors.Count > 0)
          {
            // Keep the backup so that a user can recover it manually.
            keepBackup = true;
            throw new InvalidOperationException("Не удалось восстановить некоторые файлы. Резервные копии: " + backup + ". Ошибка: " + installError.Message + ". " + String.Join("; ", rollbackErrors), installError);
          }
          throw;
        }
      }
      finally
      {
        // If rollback failed, keep all backups for recovery.
        if (Directory.Exists(staging) && !keepBackup)
          try { Directory.Delete(staging, true); } catch { }
      }
    }

    private static int SelfTest()
    {
      string root = Path.Combine(Path.GetTempPath(), "ViewCubeInstallerTest-" + Guid.NewGuid().ToString("N"));
      try
      {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Renga.exe"), "test");
        var detected = new System.Collections.Generic.List<string>();
        RengaDiscovery.Add(detected, root);
        RengaDiscovery.Add(detected, root + Path.DirectorySeparatorChar);
        RengaDiscovery.Add(detected, Path.Combine(root, "not-renga"));
        if (detected.Count != 1 || !RengaDiscovery.IsValid(detected[0])) throw new Exception("Installation detection validation failed");
        Install(root);
        string target = Path.Combine(root, "Plugins", "ViewCubeOverlay");
        foreach (string name in Files)
          if (!File.Exists(Path.Combine(target, name)) || new FileInfo(Path.Combine(target, name)).Length == 0) throw new Exception("Missing: " + name);
        string dll = Path.Combine(target, "ViewCubeOverlay.dll");
        string expected;
        using (var hash = System.Security.Cryptography.SHA256.Create())
          expected = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(dll)));
        File.WriteAllText(dll, "old version");
        File.WriteAllText(Path.Combine(target, "unrelated.txt"), "keep");
        Install(root);
        using (var hash = System.Security.Cryptography.SHA256.Create())
          if (expected != BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(dll)))) throw new Exception("Update did not replace old file");
        if (!File.Exists(Path.Combine(target, "unrelated.txt"))) throw new Exception("Unrelated file removed");
        Console.WriteLine("PASS: clean install and update in an isolated directory");
        return 0;
      }
      catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
      finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
    }
  }
}
