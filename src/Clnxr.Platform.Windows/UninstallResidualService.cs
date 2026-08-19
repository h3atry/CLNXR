using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using Clnxr.Core;
using Clnxr.Safety;

namespace Clnxr.Platform.Windows
{
    public sealed class UninstallResidualEntry
    {
        public UninstallResidualEntry(string displayName, string publisher, string version, string installLocation,
            string uninstallCommand, string registryLocation, bool isResidual, string status)
        {
            DisplayName = displayName ?? string.Empty;
            Publisher = publisher ?? string.Empty;
            Version = version ?? string.Empty;
            InstallLocation = PathRedactor.Redact(installLocation);
            UninstallCommand = PathRedactor.Redact(uninstallCommand);
            RegistryLocation = registryLocation ?? string.Empty;
            IsResidual = isResidual;
            Status = status ?? string.Empty;
        }

        public string DisplayName { get; private set; }
        public string Publisher { get; private set; }
        public string Version { get; private set; }
        public string InstallLocation { get; private set; }
        public string UninstallCommand { get; private set; }
        public string RegistryLocation { get; private set; }
        public bool IsResidual { get; private set; }
        public string Status { get; private set; }
    }

    public sealed class UninstallResidualResult
    {
        public UninstallResidualResult()
        {
            Entries = new List<UninstallResidualEntry>();
            Issues = new List<string>();
        }

        public IList<UninstallResidualEntry> Entries { get; private set; }
        public IList<string> Issues { get; private set; }
    }

    /// <summary>
    /// Inventário somente leitura de entradas de desinstalação conhecidas.
    /// Nunca remove chaves, executa comandos ou infere sobras sem um caminho
    /// de instalação declarado pela própria entrada.
    /// </summary>
    public sealed class UninstallResidualService
    {
        private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string Wow6432UninstallPath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        public UninstallResidualResult ListEntries()
        {
            UninstallResidualResult result = new UninstallResidualResult();
            ReadHive(result, Registry.CurrentUser, "HKCU", UninstallPath);
            ReadHive(result, Registry.LocalMachine, "HKLM", UninstallPath);
            ReadHive(result, Registry.LocalMachine, "HKLM", Wow6432UninstallPath);
            return result;
        }

        private static void ReadHive(UninstallResidualResult result, RegistryKey hive, string hiveName, string path)
        {
            try
            {
                using (RegistryKey root = hive.OpenSubKey(path, false))
                {
                    if (root == null) return;
                    foreach (string subKeyName in root.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey key = root.OpenSubKey(subKeyName, false))
                            {
                                if (key == null) continue;
                                string displayName = ReadString(key, "DisplayName");
                                if (string.IsNullOrWhiteSpace(displayName)) continue;

                                string publisher = ReadString(key, "Publisher");
                                string version = ReadString(key, "DisplayVersion");
                                string installLocation = NormalizeDeclaredLocation(ReadString(key, "InstallLocation"));
                                string uninstallCommand = ReadString(key, "UninstallString");
                                bool hasDeclaredLocation = !string.IsNullOrWhiteSpace(installLocation);
                                bool exists = hasDeclaredLocation && (Directory.Exists(installLocation) || File.Exists(installLocation));
                                bool reparse = exists && PathSafetyPolicy.ContainsReparsePoint(installLocation);
                                bool residual = hasDeclaredLocation && !exists && !reparse;
                                string status;
                                if (residual)
                                    status = "Candidato: InstallLocation declarado não existe";
                                else if (!hasDeclaredLocation)
                                    status = "Não avaliada: entrada sem InstallLocation";
                                else if (reparse)
                                    status = "Preservada: InstallLocation é reparse point";
                                else
                                    status = "Local de instalação presente";

                                result.Entries.Add(new UninstallResidualEntry(displayName, publisher, version,
                                    installLocation, uninstallCommand, hiveName + "\\" + path + "\\" + subKeyName,
                                    residual, status));
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Issues.Add(PathRedactor.Redact(hiveName + "\\" + path + "\\" + subKeyName + ": " + ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(PathRedactor.Redact(hiveName + "\\" + path + ": " + ex.Message));
            }
        }

        private static string ReadString(RegistryKey key, string name)
        {
            object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value == null ? string.Empty : value.ToString();
        }

        private static string NormalizeDeclaredLocation(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length >= 2 && normalized[0] == '"' && normalized[normalized.Length - 1] == '"')
                normalized = normalized.Substring(1, normalized.Length - 2).Trim();
            return Environment.ExpandEnvironmentVariables(normalized);
        }
    }
}
