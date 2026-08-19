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
            : this(scope, name, command, source, false, string.Empty, string.Empty, string.Empty)
        {
        }

        internal StartupEntry(string scope, string name, string command, string source, bool canDisable,
            string hiveName, string registrySubKey, string registryValueName)
        {
            Scope = scope ?? string.Empty;
            Name = name ?? string.Empty;
            Command = command ?? string.Empty;
            Source = source ?? string.Empty;
            CanDisable = canDisable;
            HiveName = hiveName ?? string.Empty;
            RegistrySubKey = registrySubKey ?? string.Empty;
            RegistryValueName = registryValueName ?? string.Empty;
        }

        public string Scope { get; private set; }
        public string Name { get; private set; }
        public string Command { get; private set; }
        public string Source { get; private set; }
        public bool CanDisable { get; private set; }
        internal string HiveName { get; private set; }
        internal string RegistrySubKey { get; private set; }
        internal string RegistryValueName { get; private set; }
    }

    public sealed class DisabledStartupEntry
    {
        internal DisabledStartupEntry(string backupId, string scope, string name, string command, string source,
            string hiveName, string registrySubKey, string registryValueName)
        {
            BackupId = backupId ?? string.Empty;
            Scope = scope ?? string.Empty;
            Name = name ?? string.Empty;
            Command = PathRedactor.Redact(command);
            Source = source ?? string.Empty;
            HiveName = hiveName ?? string.Empty;
            RegistrySubKey = registrySubKey ?? string.Empty;
            RegistryValueName = registryValueName ?? string.Empty;
        }

        public string BackupId { get; private set; }
        public string Scope { get; private set; }
        public string Name { get; private set; }
        public string Command { get; private set; }
        public string Source { get; private set; }
        internal string HiveName { get; private set; }
        internal string RegistrySubKey { get; private set; }
        internal string RegistryValueName { get; private set; }
    }

    public sealed class StartupMutationResult
    {
        public StartupMutationResult(bool succeeded, string message, string backupId)
        {
            Succeeded = succeeded;
            Message = PathRedactor.Redact(message);
            BackupId = backupId ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public string BackupId { get; private set; }
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
    /// Inventário de locais comuns de inicialização. A enumeração é somente
    /// leitura; a mutação separada só permite HKCU com backup reversível,
    /// nunca executa comandos nem eleva privilégios automaticamente.
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
                            result.Entries.Add(new StartupEntry(scope, name, command, source, true, hive == Registry.CurrentUser ? "HKCU" : "HKLM", subKey, name));
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

        public IList<DisabledStartupEntry> ListDisabledEntries()
        {
            List<DisabledStartupEntry> entries = new List<DisabledStartupEntry>();
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(DisabledRoot, false))
                {
                    if (root == null) return entries;
                    foreach (string backupId in root.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey backup = root.OpenSubKey(backupId, false))
                            {
                                if (backup == null) continue;
                                entries.Add(ReadDisabled(backupId, backup));
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return entries;
        }

        public StartupMutationResult Disable(StartupEntry entry)
        {
            if (entry == null) return new StartupMutationResult(false, "Entrada ausente.", string.Empty);
            if (!entry.CanDisable) return new StartupMutationResult(false, "Somente entradas de Registro HKCU podem ser desabilitadas com desfazer nesta versão.", string.Empty);
            if (!string.Equals(entry.HiveName, "HKCU", StringComparison.OrdinalIgnoreCase))
                return new StartupMutationResult(false, "Entradas HKLM exigem elevação explícita e não são alteradas automaticamente.", string.Empty);
            if (!IsAllowedSubKey(entry.RegistrySubKey) || string.IsNullOrWhiteSpace(entry.RegistryValueName) || entry.RegistryValueName.IndexOf('\\') >= 0)
                return new StartupMutationResult(false, "A origem da entrada não pertence aos locais suportados.", string.Empty);

            string backupId = Guid.NewGuid().ToString("N");
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(entry.RegistrySubKey, true))
                {
                    if (key == null) return new StartupMutationResult(false, "A chave de inicialização não está disponível para escrita.", string.Empty);
                    object value = key.GetValue(entry.RegistryValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (value == null) return new StartupMutationResult(false, "A entrada mudou desde a análise; nenhuma alteração foi feita.", string.Empty);
                    string current = value.ToString();
                    if (!string.Equals(current, entry.Command, StringComparison.Ordinal))
                        return new StartupMutationResult(false, "A entrada mudou desde a análise; nenhuma alteração foi feita.", string.Empty);

                    RegistryValueKind kind = key.GetValueKind(entry.RegistryValueName);
                    if (kind != RegistryValueKind.String && kind != RegistryValueKind.ExpandString)
                        return new StartupMutationResult(false, "A entrada não é um valor textual suportado; nenhuma alteração foi feita.", string.Empty);
                    using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(DisabledRoot + "\\" + backupId))
                    {
                        if (backup == null) return new StartupMutationResult(false, "Não foi possível criar o registro reversível local.", string.Empty);
                        backup.SetValue("Hive", "HKCU", RegistryValueKind.String);
                        backup.SetValue("SubKey", entry.RegistrySubKey, RegistryValueKind.String);
                        backup.SetValue("ValueName", entry.RegistryValueName, RegistryValueKind.String);
                        backup.SetValue("OriginalName", entry.RegistryValueName, RegistryValueKind.String);
                        backup.SetValue("Command", current, kind);
                        backup.SetValue("ValueKind", (int)kind, RegistryValueKind.DWord);
                    }

                    try
                    {
                        key.DeleteValue(entry.RegistryValueName, false);
                    }
                    catch
                    {
                        TryDeleteBackup(backupId);
                        throw;
                    }
                }
                return new StartupMutationResult(true, "Entrada HKCU desabilitada e guardada para desfazer.", backupId);
            }
            catch (Exception ex)
            {
                TryDeleteBackup(backupId);
                return new StartupMutationResult(false, "Não foi possível desabilitar a entrada: " + ex.Message, string.Empty);
            }
        }

        public StartupMutationResult Restore(DisabledStartupEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.BackupId))
                return new StartupMutationResult(false, "Backup de inicialização ausente.", string.Empty);
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(DisabledRoot, false))
                using (RegistryKey backup = root == null ? null : root.OpenSubKey(entry.BackupId, false))
                {
                    if (backup == null) return new StartupMutationResult(false, "O backup reversível não existe mais.", string.Empty);
                    string subKey = ReadValue(backup, "SubKey");
                    string valueName = ReadValue(backup, "OriginalName");
                    string command = ReadValue(backup, "Command");
                    int kindNumber = (int)backup.GetValue("ValueKind", (int)RegistryValueKind.String);
                    RegistryValueKind kind = Enum.IsDefined(typeof(RegistryValueKind), kindNumber)
                        ? (RegistryValueKind)kindNumber : RegistryValueKind.String;
                    if (!IsAllowedSubKey(subKey) || string.IsNullOrWhiteSpace(valueName) || valueName.IndexOf('\\') >= 0 ||
                        (kind != RegistryValueKind.String && kind != RegistryValueKind.ExpandString))
                        return new StartupMutationResult(false, "O backup não aponta para um local de inicialização suportado.", string.Empty);

                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKey, true))
                    {
                        if (key == null) return new StartupMutationResult(false, "A chave de inicialização não está disponível para restauração.", string.Empty);
                        if (key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) != null)
                            return new StartupMutationResult(false, "Já existe uma entrada com o mesmo nome; nenhuma alteração foi feita.", string.Empty);
                        key.SetValue(valueName, command, kind);
                    }
                }
                TryDeleteBackup(entry.BackupId);
                return new StartupMutationResult(true, "Entrada de inicialização restaurada.", entry.BackupId);
            }
            catch (Exception ex)
            {
                return new StartupMutationResult(false, "Não foi possível restaurar a entrada: " + ex.Message, string.Empty);
            }
        }

        private const string DisabledRoot = @"Software\CLNXR\DisabledStartup";

        private static DisabledStartupEntry ReadDisabled(string backupId, RegistryKey backup)
        {
            string subKey = ReadValue(backup, "SubKey");
            string valueName = ReadValue(backup, "OriginalName");
            return new DisabledStartupEntry(backupId, "Usuário", valueName, ReadValue(backup, "Command"),
                "Backup reversível CLNXR", ReadValue(backup, "Hive"), subKey, valueName);
        }

        private static string ReadValue(RegistryKey key, string name)
        {
            object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value == null ? string.Empty : value.ToString();
        }

        private static bool IsAllowedSubKey(string subKey)
        {
            return string.Equals(subKey, RunKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(subKey, RunOnceKey, StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDeleteBackup(string backupId)
        {
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(DisabledRoot, true))
                {
                    if (root != null) root.DeleteSubKeyTree(backupId, false);
                }
            }
            catch { }
        }
    }
}
