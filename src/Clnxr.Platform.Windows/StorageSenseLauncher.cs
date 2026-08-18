using System;
using System.Diagnostics;

namespace Clnxr.Platform.Windows
{
    public sealed class StorageSenseLaunchResult
    {
        public StorageSenseLaunchResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class StorageSenseLauncher
    {
        public const string SettingsUri = "ms-settings:storagepolicies";

        public StorageSenseLaunchResult OpenSettings()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(SettingsUri);
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
                return new StorageSenseLaunchResult(true, "As Configuracoes oficiais do Windows foram abertas em Storage Sense. Nenhuma limpeza foi executada pelo CLNXR.");
            }
            catch (Exception ex)
            {
                return new StorageSenseLaunchResult(false, "Nao foi possivel abrir o Storage Sense: " + ex.Message);
            }
        }
    }
}
