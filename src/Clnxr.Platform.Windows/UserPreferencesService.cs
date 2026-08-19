using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Clnxr.Platform.Windows
{
    public sealed class UserPreferences
    {
        public bool ReducedMotion { get; set; }
        public string Language { get; set; }
        public string Theme { get; set; }
        public bool UpdatesOptIn { get; set; }
    }

    /// <summary>
    /// Preferencias locais, sem telemetria e sem guardar credenciais. O formato é
    /// deliberadamente simples para continuar legível mesmo no alvo .NET Framework 4.0.
    /// </summary>
    public sealed class UserPreferencesService
    {
        private const uint SpiGetClientAreaAnimation = 0x1042;
        private readonly string filePath;

        public UserPreferencesService()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CLNXR", "settings.ini"))
        {
        }

        public UserPreferencesService(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("O caminho de preferencias e obrigatorio.", "filePath");
            this.filePath = Path.GetFullPath(filePath);
        }

        public string FilePath { get { return filePath; } }

        public UserPreferences Load()
        {
            UserPreferences defaults = CreateDefaults();
            if (!File.Exists(filePath)) return defaults;

            try
            {
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string line = (rawLine ?? string.Empty).Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    values[key] = value;
                }

                bool parsed;
                if (values.ContainsKey("reduced_motion") && bool.TryParse(values["reduced_motion"], out parsed))
                    defaults.ReducedMotion = parsed;
                if (values.ContainsKey("updates_opt_in") && bool.TryParse(values["updates_opt_in"], out parsed))
                    defaults.UpdatesOptIn = parsed;
                if (values.ContainsKey("language") && IsSafeToken(values["language"], 24))
                    defaults.Language = values["language"];
                if (values.ContainsKey("theme") && IsSafeToken(values["theme"], 32))
                    defaults.Theme = values["theme"];
            }
            catch
            {
                // Preferencias corrompidas não podem impedir a inicialização do cleaner.
            }

            return defaults;
        }

        public UserPreferences CreateDefaults()
        {
            return new UserPreferences
            {
                ReducedMotion = !IsClientAreaAnimationEnabled(),
                Language = "pt-BR",
                Theme = "dark-graphite",
                UpdatesOptIn = false
            };
        }

        public bool Save(UserPreferences preferences, out string message)
        {
            message = string.Empty;
            if (preferences == null)
            {
                message = "Preferencias ausentes.";
                return false;
            }
            if (!IsSafeToken(preferences.Language, 24) || !IsSafeToken(preferences.Theme, 32))
            {
                message = "Idioma ou tema fora do formato permitido.";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                StringBuilder content = new StringBuilder();
                content.AppendLine("# CLNXR preferencias locais; nenhum caminho pessoal ou segredo e salvo aqui.");
                content.AppendLine("version=1");
                content.AppendLine("reduced_motion=" + preferences.ReducedMotion.ToString().ToLowerInvariant());
                content.AppendLine("language=" + preferences.Language);
                content.AppendLine("theme=" + preferences.Theme);
                content.AppendLine("updates_opt_in=" + preferences.UpdatesOptIn.ToString().ToLowerInvariant());
                File.WriteAllText(filePath, content.ToString(), new UTF8Encoding(false));
                message = "Preferências salvas localmente em " + filePath + ".";
                return true;
            }
            catch (Exception ex)
            {
                message = "Não foi possível salvar preferências: " + ex.Message;
                return false;
            }
        }

        public static bool IsClientAreaAnimationEnabled()
        {
            int enabled = 1;
            try
            {
                if (!SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref enabled, 0)) return true;
                return enabled != 0;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsSafeToken(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength) return false;
            foreach (char character in value)
            {
                if (!(char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == '.'))
                    return false;
            }
            return true;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint parameter, ref int value, uint update);
    }
}
