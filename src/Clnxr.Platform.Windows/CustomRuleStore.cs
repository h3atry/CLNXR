using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Clnxr.Platform.Windows
{
    internal sealed class CustomRuleRecord
    {
        public string ruleId { get; set; }
        public string version { get; set; }
        public string name { get; set; }
        public string rootPath { get; set; }
        public int minimumAgeDays { get; set; }
        public string[] extensions { get; set; }
        public string[] exclusions { get; set; }
        public string attribution { get; set; }
        public string signatureStatus { get; set; }
    }

    internal sealed class CustomRuleDocument
    {
        public string schemaVersion { get; set; }
        public List<CustomRuleRecord> rules { get; set; }
    }

    public sealed class CustomRuleStore
    {
        public const string SchemaVersion = "clnxr.custom-rules.v1";
        private readonly string path;

        public CustomRuleStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException("path");
            this.path = path;
        }

        public static CustomRuleStore CreateDefault()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CLNXR", "Rules");
            return new CustomRuleStore(Path.Combine(root, "custom-rules.v1.json"));
        }

        public IList<CustomRuleDefinition> List()
        {
            if (!File.Exists(path)) return new List<CustomRuleDefinition>().AsReadOnly();
            CustomRuleDocument document;
            using (StreamReader reader = new StreamReader(path))
                document = new JavaScriptSerializer().Deserialize<CustomRuleDocument>(reader.ReadToEnd());
            if (document == null || !string.Equals(document.schemaVersion, SchemaVersion, StringComparison.Ordinal) || document.rules == null)
                throw new InvalidOperationException("Arquivo de regras personalizadas ausente, corrompido ou em esquema incompatível.");

            List<CustomRuleDefinition> definitions = new List<CustomRuleDefinition>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CustomRuleRecord record in document.rules)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.ruleId) || !ids.Add(record.ruleId))
                    throw new InvalidOperationException("Arquivo de regras personalizadas contém ID ausente ou duplicado.");
                if (!record.ruleId.StartsWith("custom-", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(record.rootPath))
                    throw new InvalidOperationException("Arquivo de regras personalizadas contém uma origem inválida.");
                definitions.Add(new CustomRuleDefinition(record.ruleId, record.version ?? "1", record.name ?? string.Empty, record.rootPath,
                    Math.Max(0, record.minimumAgeDays), record.extensions ?? new string[0], record.exclusions ?? new string[0], record.attribution ?? string.Empty));
            }
            return definitions.AsReadOnly();
        }

        public void Save(CustomRuleDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            IList<CustomRuleDefinition> definitions = List().Where(item => !string.Equals(item.RuleId, definition.RuleId, StringComparison.OrdinalIgnoreCase)).ToList();
            definitions.Add(definition);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            CustomRuleDocument document = new CustomRuleDocument
            {
                schemaVersion = SchemaVersion,
                rules = definitions.Select(ToRecord).ToList()
            };
            string json = new JavaScriptSerializer().Serialize(document);
            File.WriteAllText(path, json);
        }

        public bool Delete(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return false;
            IList<CustomRuleDefinition> definitions = List();
            List<CustomRuleDefinition> remaining = definitions.Where(item => !string.Equals(item.RuleId, ruleId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (remaining.Count == definitions.Count) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            CustomRuleDocument document = new CustomRuleDocument
            {
                schemaVersion = SchemaVersion,
                rules = remaining.Select(ToRecord).ToList()
            };
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(document));
            return true;
        }

        private static CustomRuleRecord ToRecord(CustomRuleDefinition definition)
        {
            return new CustomRuleRecord
            {
                ruleId = definition.RuleId,
                version = definition.Version,
                name = definition.Name,
                rootPath = definition.RootPath,
                minimumAgeDays = definition.MinimumAgeDays,
                extensions = definition.Extensions.ToArray(),
                exclusions = definition.Exclusions.ToArray(),
                attribution = definition.Attribution,
                signatureStatus = definition.SignatureStatus
            };
        }
    }
}
