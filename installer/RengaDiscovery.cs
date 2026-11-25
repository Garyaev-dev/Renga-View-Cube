using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ViewCubeInstaller
{
  internal static class RengaDiscovery
  {
    internal static bool IsValid(string path)
    {
      try { return !String.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "Renga.exe")); }
      catch (Exception) { return false; }
    }

    internal static List<string> FindInstallations()
    {
      var results = new List<string>();
      foreach (var process in Process.GetProcessesByName("Renga"))
      {
        try { Add(results, Path.GetDirectoryName(process.MainModule.FileName)); }
        catch (Exception) { }
        finally { process.Dispose(); }
      }
      foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
          FindInRegistry(results, hive, view);
      foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"C:\Soft" })
        foreach (string name in new[] { "Renga Professional", "Renga" })
          if (!String.IsNullOrWhiteSpace(root)) Add(results, Path.Combine(root, name));
      return results;
    }

    private static void FindInRegistry(List<string> results, RegistryHive hive, RegistryView view)
    {
      try
      {
        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
        using (RegistryKey uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
        {
          if (uninstall == null) return;
          foreach (string subkey in uninstall.GetSubKeyNames())
          {
            try
            {
              using (RegistryKey app = uninstall.OpenSubKey(subkey))
              {
                if (app == null || !Convert.ToString(app.GetValue("DisplayName")).StartsWith("Renga", StringComparison.OrdinalIgnoreCase)) continue;
                Add(results, Convert.ToString(app.GetValue("InstallLocation")));
                string icon = Convert.ToString(app.GetValue("DisplayIcon")).Trim().Trim('"');
                int comma = icon.LastIndexOf(',');
                if (comma > 0) icon = icon.Substring(0, comma).Trim().Trim('"');
                if (String.Equals(Path.GetFileName(icon), "Renga.exe", StringComparison.OrdinalIgnoreCase)) Add(results, Path.GetDirectoryName(icon));
              }
            }
            catch (Exception) { }
          }
        }
      }
      catch (Exception) { }
    }

    internal static void Add(List<string> results, string path)
    {
      try
      {
        if (!IsValid(path)) return;
        string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (string existing in results)
          if (String.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)) return;
        results.Add(normalized);
      }
      catch (Exception) { }
    }
  }
}
