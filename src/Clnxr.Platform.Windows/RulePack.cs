using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using Clnxr.Core;

namespace Clnxr.Platform.Windows
{
    internal sealed class WindowsRulePackDocument
    {
        public string schemaVersion { get; set; }
        public string catalogVersion { get; set; }
        public List<WindowsRulePackEntry> rules { get; set; }
    }

    internal sealed class WindowsRulePackEntry
    {
        public string ruleId { get; set; }
        public string version { get; set; }
        public string category { get; set; }
        public string explanation { get; set; }
        public string risk { get; set; }
        public string relativePath { get; set; }
        public string filter { get; set; }
        public string[] profiles { get; set; }
        public string[] requiredClosedProcesses { get; set; }
        public int minimumAgeDays { get; set; }
        public bool systemOnly { get; set; }
        public string pathBase { get; set; }
    }

    internal static class WindowsRulePack
    {
        private const string ResourceName = "Clnxr.Platform.Windows.rules.windows.v1.json";
        private const string SchemaVersion = "clnxr.rules.windows.v1";

        public static IList<WindowsRuleTemplate> Load()
        {
            WindowsRulePackDocument document;
            Assembly assembly = typeof(WindowsRulePack).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null) throw new InvalidOperationException("Pacote declarativo de regras Windows ausente: " + ResourceName);
                using (StreamReader reader = new StreamReader(stream))
                {
                    document = new JavaScriptSerializer().Deserialize<WindowsRulePackDocument>(reader.ReadToEnd());
                }
            }

            return BuildTemplates(document);
        }

        internal static IList<WindowsRuleTemplate> BuildTemplates(WindowsRulePackDocument document)
        {
            Validate(document);
            List<WindowsRuleTemplate> templates = new List<WindowsRuleTemplate>();
            foreach (WindowsRulePackEntry entry in document.rules)
            {
                RiskLevel risk;
                if (!Enum.TryParse<RiskLevel>(entry.risk, true, out risk))
                    throw new InvalidOperationException("Risco invalido na regra " + entry.ruleId + ".");

                WindowsPathBase pathBase = ParsePathBase(entry.pathBase);
                string relativePath = entry.relativePath ?? string.Empty;
                if (entry.systemOnly && pathBase == WindowsPathBase.WindowsRoot)
                    relativePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), relativePath);

                RuleActionKind kind = string.IsNullOrEmpty(entry.filter) ? RuleActionKind.DirectoryContents : RuleActionKind.MatchingFiles;
                Rule rule = new Rule(entry.ruleId, entry.version, entry.category, entry.explanation, risk, kind,
                    entry.profiles ?? new string[0], entry.requiredClosedProcesses ?? new string[0], entry.minimumAgeDays);
                templates.Add(new WindowsRuleTemplate(rule, relativePath, entry.filter, entry.systemOnly, pathBase));
            }

            return templates.AsReadOnly();
        }

        internal static void Validate(WindowsRulePackDocument document)
        {
            if (document == null) throw new InvalidOperationException("Pacote declarativo de regras Windows vazio.");
            if (!string.Equals(document.schemaVersion, SchemaVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Schema de regras Windows nao suportado: " + document.schemaVersion);
            if (string.IsNullOrWhiteSpace(document.catalogVersion))
                throw new InvalidOperationException("Pacote de regras Windows sem versao de catalogo.");
            if (document.rules == null || document.rules.Count == 0)
                throw new InvalidOperationException("Pacote de regras Windows sem regras.");

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WindowsRulePackEntry entry in document.rules)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ruleId) || !ids.Add(entry.ruleId))
                    throw new InvalidOperationException("Pacote de regras Windows contem ID ausente ou duplicado.");
                if (string.IsNullOrWhiteSpace(entry.version) || string.IsNullOrWhiteSpace(entry.relativePath))
                    throw new InvalidOperationException("Regra " + entry.ruleId + " sem versao ou caminho.");
                if (entry.minimumAgeDays < 0)
                    throw new InvalidOperationException("Regra " + entry.ruleId + " com idade minima negativa.");
                if (entry.profiles == null || entry.profiles.Length == 0)
                    throw new InvalidOperationException("Regra " + entry.ruleId + " sem perfil permitido.");
                if (entry.systemOnly && !string.Equals(entry.pathBase, "WindowsRoot", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Regra de sistema " + entry.ruleId + " precisa usar WindowsRoot.");
            }
        }

        private static WindowsPathBase ParsePathBase(string value)
        {
            if (string.Equals(value, "RoamingAppData", StringComparison.OrdinalIgnoreCase)) return WindowsPathBase.RoamingAppData;
            if (string.Equals(value, "UserProfile", StringComparison.OrdinalIgnoreCase)) return WindowsPathBase.UserProfile;
            if (string.Equals(value, "WindowsRoot", StringComparison.OrdinalIgnoreCase)) return WindowsPathBase.WindowsRoot;
            if (string.Equals(value, "VolumeRoot", StringComparison.OrdinalIgnoreCase)) return WindowsPathBase.VolumeRoot;
            return WindowsPathBase.LocalAppData;
        }
    }
}
