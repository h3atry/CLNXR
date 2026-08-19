using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Clnxr.Core;
using Clnxr.Safety;

namespace Clnxr.Platform.Windows
{
    public sealed class CustomRuleDraft
    {
        public CustomRuleDraft(string name, string rootPath, int minimumAgeDays,
            IEnumerable<string> extensions, IEnumerable<string> exclusions, string attribution)
        {
            Name = name ?? string.Empty;
            RootPath = rootPath ?? string.Empty;
            MinimumAgeDays = minimumAgeDays;
            Extensions = new ReadOnlyCollection<string>((extensions ?? Enumerable.Empty<string>()).ToList());
            Exclusions = new ReadOnlyCollection<string>((exclusions ?? Enumerable.Empty<string>()).ToList());
            Attribution = attribution ?? string.Empty;
        }

        public string Name { get; private set; }
        public string RootPath { get; private set; }
        public int MinimumAgeDays { get; private set; }
        public ReadOnlyCollection<string> Extensions { get; private set; }
        public ReadOnlyCollection<string> Exclusions { get; private set; }
        public string Attribution { get; private set; }
    }

    public sealed class CustomRuleDefinition
    {
        internal CustomRuleDefinition(string ruleId, string version, string name, string rootPath,
            int minimumAgeDays, IEnumerable<string> extensions, IEnumerable<string> exclusions, string attribution)
        {
            RuleId = ruleId;
            Version = version;
            Name = name;
            RootPath = rootPath;
            MinimumAgeDays = minimumAgeDays;
            Extensions = new ReadOnlyCollection<string>((extensions ?? Enumerable.Empty<string>()).ToList());
            Exclusions = new ReadOnlyCollection<string>((exclusions ?? Enumerable.Empty<string>()).ToList());
            Attribution = attribution ?? string.Empty;
            SignatureStatus = "unsigned";
        }

        public string RuleId { get; private set; }
        public string Version { get; private set; }
        public string Name { get; private set; }
        public string RootPath { get; private set; }
        public int MinimumAgeDays { get; private set; }
        public ReadOnlyCollection<string> Extensions { get; private set; }
        public ReadOnlyCollection<string> Exclusions { get; private set; }
        public string Attribution { get; private set; }
        public string SignatureStatus { get; private set; }

        public Rule ToRule()
        {
            return new Rule(RuleId, Version, string.IsNullOrWhiteSpace(Name) ? "Regra personalizada" : Name,
                "A prévia enumerou somente os arquivos explicitamente incluídos; regra sempre ADVANCED e unsigned.",
                RiskLevel.Advanced, RuleActionKind.MatchingFiles, new[] { "Personalizado" }, new string[0], MinimumAgeDays);
        }
    }

    public sealed class CustomRulePreview
    {
        public CustomRulePreview(CustomRuleDefinition definition, Finding finding, IEnumerable<string> examples,
            IEnumerable<string> issues, bool canSave)
        {
            Definition = definition;
            Finding = finding;
            Examples = new ReadOnlyCollection<string>((examples ?? Enumerable.Empty<string>()).ToList());
            Issues = new ReadOnlyCollection<string>((issues ?? Enumerable.Empty<string>()).ToList());
            CanSave = canSave;
        }

        public CustomRuleDefinition Definition { get; private set; }
        public Finding Finding { get; private set; }
        public ReadOnlyCollection<string> Examples { get; private set; }
        public ReadOnlyCollection<string> Issues { get; private set; }
        public bool CanSave { get; private set; }
    }

    public sealed class CustomRuleService
    {
        private const int MaximumPreviewFiles = 50000;
        private const int MaximumExamples = 12;
        private readonly PathSafetyPolicy safetyPolicy;

        public CustomRuleService(PathSafetyPolicy safetyPolicy)
        {
            if (safetyPolicy == null) throw new ArgumentNullException("safetyPolicy");
            this.safetyPolicy = safetyPolicy;
        }

        public CustomRulePreview Preview(CustomRuleDraft draft, CancellationToken cancellationToken, Action<string> progress)
        {
            List<string> issues = new List<string>();
            CustomRuleDefinition definition = null;
            string root = string.Empty;
            try
            {
                ValidateDraft(draft);
                root = PathSafetyPolicy.Normalize(draft.RootPath);
                if (!Directory.Exists(root)) throw new InvalidOperationException("A pasta escolhida não existe.");
                if (PathSafetyPolicy.ContainsReparsePoint(root)) throw new InvalidOperationException("A pasta escolhida contém link, junction ou outro reparse point.");
                if (IsUserProfileRoot(root)) throw new InvalidOperationException("A raiz do perfil pessoal não pode ser uma regra personalizada.");

                Rule provisionalRule = new Rule("custom-preview", "1", draft.Name, "Prévia de regra personalizada.",
                    RiskLevel.Advanced, RuleActionKind.MatchingFiles, new[] { "Personalizado" }, new string[0], draft.MinimumAgeDays);
                Finding provisional = new Finding(null, provisionalRule, Path.GetPathRoot(root), root, root, string.Empty, 0, 0);
                SafetyDecision decision = safetyPolicy.ValidateFinding(provisional);
                if (!decision.Allowed) throw new InvalidOperationException(decision.Reason);

                definition = BuildDefinition(draft, root);
                Rule rule = definition.ToRule();
                List<string> selectedFiles = new List<string>();
                long bytes = 0;
                EnumerateFiles(root, root, draft, selectedFiles, ref bytes, issues, cancellationToken, progress);
                if (cancellationToken.IsCancellationRequested)
                    issues.Add("Prévia cancelada antes de concluir a enumeração.");

                Finding finding = selectedFiles.Count == 0
                    ? null
                    : new Finding(null, rule, Path.GetPathRoot(root), root, root, string.Empty, bytes, selectedFiles.Count, selectedFiles);
                IEnumerable<string> examples = selectedFiles.Take(MaximumExamples).Select(PathRedactor.Redact);
                bool canSave = !cancellationToken.IsCancellationRequested && issues.Count == 0 && finding != null;
                return new CustomRulePreview(definition, finding, examples, issues, canSave);
            }
            catch (Exception ex)
            {
                issues.Add(PathRedactor.Redact(ex.Message));
                if (definition == null && !string.IsNullOrWhiteSpace(root))
                    definition = BuildDefinitionForFailure(draft, root);
                return new CustomRulePreview(definition, null, new string[0], issues, false);
            }
        }

        public CustomRuleDefinition ValidateAndCreate(CustomRuleDraft draft)
        {
            ValidateDraft(draft);
            string root = PathSafetyPolicy.Normalize(draft.RootPath);
            if (!Directory.Exists(root)) throw new InvalidOperationException("A pasta escolhida não existe.");
            if (PathSafetyPolicy.ContainsReparsePoint(root)) throw new InvalidOperationException("A pasta escolhida contém link, junction ou outro reparse point.");
            if (IsUserProfileRoot(root)) throw new InvalidOperationException("A raiz do perfil pessoal não pode ser uma regra personalizada.");
            Finding finding = new Finding(null, new Rule("custom-validation", "1", draft.Name, "Validação", RiskLevel.Advanced,
                RuleActionKind.MatchingFiles, new[] { "Personalizado" }, new string[0], draft.MinimumAgeDays),
                Path.GetPathRoot(root), root, root, string.Empty, 0, 0);
            SafetyDecision decision = safetyPolicy.ValidateFinding(finding);
            if (!decision.Allowed) throw new InvalidOperationException(decision.Reason);
            return BuildDefinition(draft, root);
        }

        private static void ValidateDraft(CustomRuleDraft draft)
        {
            if (draft == null) throw new ArgumentNullException("draft");
            if (string.IsNullOrWhiteSpace(draft.Name)) throw new InvalidOperationException("A regra personalizada precisa de um nome.");
            if (draft.Name.Length > 80) throw new InvalidOperationException("O nome da regra personalizada é muito longo.");
            if (draft.MinimumAgeDays < 0) throw new InvalidOperationException("A idade mínima não pode ser negativa.");
            if (string.IsNullOrWhiteSpace(draft.RootPath) || !Path.IsPathRooted(draft.RootPath))
                throw new InvalidOperationException("A pasta da regra personalizada precisa ser um caminho absoluto.");
            foreach (string extension in draft.Extensions ?? new ReadOnlyCollection<string>(new List<string>()))
            {
                if (string.IsNullOrWhiteSpace(extension)) continue;
                string normalized = extension.Trim();
                if (!normalized.StartsWith(".", StringComparison.Ordinal)) normalized = "." + normalized;
                if (normalized.IndexOfAny(new[] { '\\', '/', '*', '?' }) >= 0)
                    throw new InvalidOperationException("Extensões personalizadas não podem conter caminho ou curinga.");
            }
            foreach (string exclusion in draft.Exclusions ?? new ReadOnlyCollection<string>(new List<string>()))
            {
                if (string.IsNullOrWhiteSpace(exclusion)) continue;
                if (Path.IsPathRooted(exclusion) || exclusion.IndexOf("..", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("Exclusões personalizadas precisam ser relativas e não podem sair da raiz.");
            }
        }

        private void EnumerateFiles(string root, string current, CustomRuleDraft draft, IList<string> selectedFiles,
            ref long bytes, IList<string> issues, CancellationToken cancellationToken, Action<string> progress)
        {
            if (selectedFiles.Count >= MaximumPreviewFiles) {
                issues.Add("A prévia foi limitada a " + MaximumPreviewFiles + " arquivos; reduza o escopo antes de salvar.");
                return;
            }
            if (cancellationToken.IsCancellationRequested) return;
            if (PathSafetyPolicy.ContainsReparsePoint(current))
            {
                issues.Add("Reparse point ignorado durante a prévia: " + PathRedactor.Redact(current));
                return;
            }

            string[] entries;
            try { entries = Directory.GetFileSystemEntries(current); }
            catch (Exception ex)
            {
                issues.Add("Pasta ignorada sem forçar acesso: " + PathRedactor.Redact(ex.Message));
                return;
            }

            foreach (string entry in entries)
            {
                if (cancellationToken.IsCancellationRequested) return;
                if (IsExcluded(entry, root, draft.Exclusions)) continue;
                if (PathSafetyPolicy.ContainsReparsePoint(entry))
                {
                    issues.Add("Reparse point ignorado durante a prévia: " + PathRedactor.Redact(entry));
                    continue;
                }
                if (Directory.Exists(entry))
                {
                    EnumerateFiles(root, entry, draft, selectedFiles, ref bytes, issues, cancellationToken, progress);
                    continue;
                }
                if (!File.Exists(entry) || !MatchesExtension(entry, draft.Extensions)) continue;

                try
                {
                    FileInfo info = new FileInfo(entry);
                    if (draft.MinimumAgeDays > 0 && info.LastWriteTimeUtc > DateTime.UtcNow.Subtract(TimeSpan.FromDays(draft.MinimumAgeDays))) continue;
                    selectedFiles.Add(entry);
                    bytes += info.Length;
                    if (progress != null && selectedFiles.Count % 64 == 0) progress("Prévia personalizada: " + PathRedactor.Redact(entry));
                }
                catch (Exception ex)
                {
                    issues.Add("Arquivo ignorado sem forçar acesso: " + PathRedactor.Redact(ex.Message));
                }
            }
        }

        private static bool MatchesExtension(string path, IEnumerable<string> extensions)
        {
            List<string> normalized = (extensions ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().StartsWith(".", StringComparison.Ordinal) ? value.Trim() : "." + value.Trim())
                .ToList();
            return normalized.Count == 0 || normalized.Any(extension => string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsExcluded(string path, string root, IEnumerable<string> exclusions)
        {
            string relative;
            try { relative = Path.GetFullPath(path).Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return true; }
            string normalized = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
            foreach (string exclusion in exclusions ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(exclusion)) continue;
                string candidate = exclusion.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);
                if (string.Equals(normalized, candidate, StringComparison.OrdinalIgnoreCase) || normalized.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsUserProfileRoot(string root)
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(profile)) return false;
            return string.Equals(PathSafetyPolicy.Normalize(profile), PathSafetyPolicy.Normalize(root), StringComparison.OrdinalIgnoreCase);
        }

        private static CustomRuleDefinition BuildDefinition(CustomRuleDraft draft, string root)
        {
            string seed = draft.Name + "\n" + root;
            string hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(seed))).Replace("-", string.Empty).ToLowerInvariant();
            }
            return new CustomRuleDefinition("custom-" + hash.Substring(0, 16), "1", draft.Name, root,
                draft.MinimumAgeDays, NormalizeValues(draft.Extensions), NormalizeValues(draft.Exclusions), draft.Attribution);
        }

        private static CustomRuleDefinition BuildDefinitionForFailure(CustomRuleDraft draft, string root)
        {
            IEnumerable<string> extensions = draft == null ? new string[0] : draft.Extensions.Cast<string>();
            IEnumerable<string> exclusions = draft == null ? new string[0] : draft.Exclusions.Cast<string>();
            return new CustomRuleDefinition("custom-invalid", "1", draft == null ? "" : draft.Name, root,
                draft == null ? 0 : draft.MinimumAgeDays, extensions, exclusions,
                draft == null ? "" : draft.Attribution);
        }

        private static IEnumerable<string> NormalizeValues(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
