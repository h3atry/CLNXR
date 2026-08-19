using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using Clnxr.Core;
using Clnxr.Safety;

namespace Clnxr.Platform.Windows
{
    public sealed class StartupEntry
    {
        public StartupEntry(string scope, string name, string command, string source)
        {
            Scope = scope ?? string.Empty;
            Name = name ?? string.Empty;
            Command = command ?? string.Empty;
            Source = source ?? string.Empty;
        }

        public string Scope { get; private set; }
        public string Name { get; private set; }
        public string Command { get; private set; }
        public string Source { get; private set; }
    }

    public sealed class StartupExplorerResult
    {
        public StartupExplorerResult()
        {
            Entries = new List<StartupEntry>();
            Issues = new List<string>();
        }

        public IList<StartupEntry> Entries { get; private set; }
        public IList<string> Issues { get; private set; }
    }

    /// <summary>
    /// Inventory-only view of common Windows startup locations. It never writes,
    /// disables, deletes, executes, or elevates an entry.
    /// </summary>
    public sealed class StartupExplorerService
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RunOnceKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";

        public StartupExplorerResult ListEntries()
        {
            StartupExplorerResult result = new StartupExplorerResult();
            ReadRegistry(result, Registry.CurrentUser, "Usuário", RunKey, "HKCU Run");
            ReadRegistry(result, Registry.CurrentUser, "Usuário", RunOnceKey, "HKCU RunOnce");
            ReadRegistry(result, Registry.LocalMachine, "Computador", RunKey, "HKLM Run");
            ReadRegistry(result, Registry.LocalMachine, "Computador", RunOnceKey, "HKLM RunOnce");
            ReadStartupFolder(result, Environment.SpecialFolder.Startup, "Usuário");
            ReadStartupFolder(result, Environment.SpecialFolder.CommonStartup, "Todos os usuários");
            return result;
        }

        private static void ReadRegistry(StartupExplorerResult result, RegistryKey hive, string scope, string subKey, string source)
        {
            try
            {
                using (RegistryKey key = hive.OpenSubKey(subKey, false))
                {
                    if (key == null) return;
                    foreach (string name in key.GetValueNames())
                    {
                        try
                        {
                            object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                            string command = value == null ? string.Empty : value.ToString();
                            result.Entries.Add(new StartupEntry(scope, name, command, source));
                        }
                        catch (Exception ex)
                        {
                            result.Issues.Add(PathRedactor.Redact(source + ": não foi possível ler " + name + ": " + ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(PathRedactor.Redact(source + ": não foi possível enumerar: " + ex.Message));
            }
        }

        private static void ReadStartupFolder(StartupExplorerResult result, Environment.SpecialFolder folder, string scope)
        {
            try
            {
                string path = Environment.GetFolderPath(folder);
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
                foreach (string item in Directory.GetFileSystemEntries(path))
                {
                    if (PathSafetyPolicy.ContainsReparsePoint(item))
                    {
                        result.Issues.Add(PathRedactor.Redact("Inicialização ignorou reparse point: " + item));
                        continue;
                    }
                    result.Entries.Add(new StartupEntry(scope, Path.GetFileName(item), item, "Pasta de Inicialização"));
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(PathRedactor.Redact("Pasta de Inicialização: não foi possível enumerar: " + ex.Message));
            }
        }
    }
}
