using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Clnxr.Core;
using Clnxr.Safety;

namespace Clnxr.Platform.Windows
{
public enum ScanProfile
    {
        Safe,
        Complete,
        Gaming,
        Developer,
        Personalized
    }

    public sealed class ScanOptions
    {
        public ScanOptions(ScanProfile profile)
            : this(profile, null, null)
        {
        }

        public ScanOptions(ScanProfile profile, IEnumerable<string> selectedRuleIds)
            : this(profile, selectedRuleIds, null)
        {
        }

        public ScanOptions(ScanProfile profile, IEnumerable<string> selectedRuleIds, IEnumerable<CustomRuleDefinition> customRules)
        {
            Profile = profile;
            SelectedRuleIds = new List<string>(selectedRuleIds ?? Enumerable.Empty<string>()).AsReadOnly();
            CustomRules = new List<CustomRuleDefinition>(customRules ?? Enumerable.Empty<CustomRuleDefinition>()).AsReadOnly();
        }

        public ScanProfile Profile { get; private set; }
        public IList<string> SelectedRuleIds { get; private set; }
        public IList<CustomRuleDefinition> CustomRules { get; private set; }
        public string ProfileName
        {
            get
            {
                if (Profile == ScanProfile.Complete) return "Completo";
                if (Profile == ScanProfile.Gaming) return "Jogos";
                if (Profile == ScanProfile.Developer) return "Desenvolvedor";
                if (Profile == ScanProfile.Personalized) return "Personalizado";
                return "Seguro";
            }
        }
    }

    internal sealed class WindowsRuleTemplate
    {
        public WindowsRuleTemplate(Rule rule, string relativePath, string filter, bool systemOnly)
            : this(rule, relativePath, filter, systemOnly, WindowsPathBase.LocalAppData)
        {
        }

        public WindowsRuleTemplate(Rule rule, string relativePath, string filter, bool systemOnly, WindowsPathBase pathBase)
        {
            Rule = rule;
            RelativePath = relativePath ?? string.Empty;
            Filter = filter ?? string.Empty;
            SystemOnly = systemOnly;
            PathBase = pathBase;
        }

        public Rule Rule { get; private set; }
        public string RelativePath { get; private set; }
        public string Filter { get; private set; }
        public bool SystemOnly { get; private set; }
        public WindowsPathBase PathBase { get; private set; }
    }

    internal enum WindowsPathBase
    {
        LocalAppData,
        RoamingAppData,
        UserProfile,
        WindowsRoot,
        VolumeRoot
    }

    public static class WindowsRuleCatalog
    {
        public const string CatalogVersion = "0.2.0";

        internal static IList<WindowsRuleTemplate> GetTemplates(ScanProfile profile)
        {
            return GetTemplates(profile, null);
        }

        internal static IList<WindowsRuleTemplate> GetTemplates(ScanProfile profile, IEnumerable<string> selectedRuleIds)
        {
            IList<WindowsRuleTemplate> templates = WindowsRulePack.Load();
            if (profile == ScanProfile.Personalized)
            {
                HashSet<string> selected = new HashSet<string>(selectedRuleIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                return templates.Where(template => selected.Contains(template.Rule.RuleId)).ToList();
            }

            return templates.Where(template => template.Rule.Profiles.Contains(ProfileName(profile), StringComparer.OrdinalIgnoreCase)).ToList();
        }

        public static IList<Rule> GetAllRules()
        {
            List<WindowsRuleTemplate> templates = new List<WindowsRuleTemplate>();
            foreach (ScanProfile item in Enum.GetValues(typeof(ScanProfile))) templates.AddRange(GetTemplates(item));
            return templates.Select(template => template.Rule).GroupBy(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
        }

        public static IList<Rule> GetRules(ScanProfile profile)
        {
            return GetTemplates(profile).Select(template => template.Rule).ToList();
        }

        public static IList<Rule> GetRules(ScanProfile profile, IEnumerable<string> selectedRuleIds)
        {
            return GetTemplates(profile, selectedRuleIds).Select(template => template.Rule).ToList();
        }

        private static string ProfileName(ScanProfile profile)
        {
            if (profile == ScanProfile.Complete) return "Completo";
                if (profile == ScanProfile.Gaming) return "Jogos";
                if (profile == ScanProfile.Developer) return "Desenvolvedor";
                if (profile == ScanProfile.Personalized) return "Personalizado";
                return "Seguro";
        }
    }

    public sealed class WindowsCandidateScanner
    {
        private static readonly string[] ExcludedProfiles = { "Default", "Default User", "Public", "All Users" };
        private readonly PathSafetyPolicy safetyPolicy;

        public WindowsCandidateScanner(PathSafetyPolicy safetyPolicy)
        {
            if (safetyPolicy == null) throw new ArgumentNullException("safetyPolicy");
            this.safetyPolicy = safetyPolicy;
        }

        public ScanSession Scan(ScanOptions options, CancellationToken cancellationToken, Action<string> progress)
        {
            if (options == null) throw new ArgumentNullException("options");
            ScanSession session = new ScanSession(options.ProfileName, WindowsRuleCatalog.CatalogVersion);
            session.BeginScan();
            IList<WindowsRuleTemplate> rules = WindowsRuleCatalog.GetTemplates(options.Profile, options.SelectedRuleIds);
            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory);

            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        session.Cancel();
                        return session;
                    }
                    if (!drive.IsReady || (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)) continue;

                    string volumeRoot = drive.RootDirectory.FullName;
                    Report(progress, "Analisando " + volumeRoot);
                    foreach (string profile in GetProfiles(volumeRoot, session))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            session.Cancel();
                            return session;
                        }

                        foreach (WindowsRuleTemplate rule in rules.Where(r => !r.SystemOnly && !r.Rule.RuleId.StartsWith("nvidia-system", StringComparison.OrdinalIgnoreCase)))
                        {
                            string profileBase = GetProfileBase(profile, rule.PathBase);
                            string targetPattern = Path.Combine(profileBase, rule.RelativePath);
                            foreach (string target in ExpandDirectoryPattern(targetPattern))
                                AddIfEligible(session, rule, drive.Name, target, target, cancellationToken);
                        }
                    }

                    foreach (WindowsRuleTemplate rule in rules.Where(r => r.SystemOnly))
                    {
                        if (string.Equals(volumeRoot, systemRoot, StringComparison.OrdinalIgnoreCase))
                            AddIfEligible(session, rule, drive.Name, rule.RelativePath, rule.RelativePath, cancellationToken);
                    }

                    foreach (WindowsRuleTemplate rule in rules.Where(r => r.Rule.RuleId.StartsWith("nvidia-system", StringComparison.OrdinalIgnoreCase)))
                    {
                        string target = Path.Combine(volumeRoot, rule.RelativePath);
                        AddIfEligible(session, rule, drive.Name, target, target, cancellationToken);
                    }
                }

                if (options.CustomRules.Count > 0)
                {
                    if (options.Profile != ScanProfile.Personalized)
                    {
                        session.AddIssue("custom-rules", "Regras personalizadas só podem ser analisadas pelo perfil Personalizado.");
                    }
                    else
                    {
                        CustomRuleService customService = new CustomRuleService(safetyPolicy);
                        foreach (CustomRuleDefinition customRule in options.CustomRules)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                session.Cancel();
                                return session;
                            }
                            CustomRulePreview preview = customService.Preview(
                                new CustomRuleDraft(customRule.Name, customRule.RootPath, customRule.MinimumAgeDays,
                                    customRule.Extensions, customRule.Exclusions, customRule.Attribution),
                                cancellationToken, delegate(string message) { Report(progress, message); });
                            foreach (string issue in preview.Issues) session.AddIssue(customRule.RuleId, issue);
                            if (preview.CanSave && preview.Finding != null) session.AddFinding(preview.Finding);
                        }
                    }
                }

                session.CompleteScan();
                return session;
            }
            catch (Exception ex)
            {
                session.Fail("scan", "Falha inesperada durante a analise: " + ex.Message);
                return session;
            }
        }

        public static IList<string> ExpandDirectoryPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return new List<string>();

            bool hasWildcard = pattern.IndexOfAny(new[] { '*', '?' }) >= 0;
            string fullPath = hasWildcard
                ? (Path.IsPathRooted(pattern) ? pattern : Path.Combine(Environment.CurrentDirectory, pattern))
                : Path.GetFullPath(pattern);
            string root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return new List<string>();

            List<string> candidates = new List<string> { root };
            string relative = fullPath.Substring(root.Length);
            string[] segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string segment in segments)
            {
                List<string> next = new List<string>();
                foreach (string candidate in candidates)
                {
                    try
                    {
                        if (segment.IndexOfAny(new[] { '*', '?' }) >= 0)
                            next.AddRange(Directory.GetDirectories(candidate, segment, SearchOption.TopDirectoryOnly));
                        else
                        {
                            string child = Path.Combine(candidate, segment);
                            if (Directory.Exists(child)) next.Add(child);
                        }
                    }
                    catch
                    {
                        // Um caminho sem acesso nao vira achado; o scan continua nas demais origens.
                    }
                }

                candidates = next.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (candidates.Count == 0) break;
            }

            return candidates;
        }

        private void AddIfEligible(ScanSession session, WindowsRuleTemplate template, string volume, string sourceRoot,
            string targetPath, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;
            if (!Directory.Exists(targetPath)) return;

            Finding provisional = new Finding(null, template.Rule, volume, sourceRoot, targetPath, template.Filter, 0, 0);
            SafetyDecision decision = safetyPolicy.ValidateFinding(provisional);
            if (!decision.Allowed)
            {
                session.AddIssue(template.Rule.RuleId, "Alvo ignorado pela politica: " + decision.Reason);
                return;
            }

            MeasurementResult measurement = template.Rule.ActionKind == RuleActionKind.DirectoryContents
                ? FileMeasurement.MeasureDirectory(targetPath, cancellationToken, template.Rule.MinimumAgeDays)
                : FileMeasurement.MeasureMatchingFiles(targetPath, template.Filter, cancellationToken, template.Rule.MinimumAgeDays);

            foreach (string issue in measurement.Issues) session.AddIssue(template.Rule.RuleId, issue);
            if (measurement.WasCancelled) return;
            if (measurement.FileCount == 0) return;

            session.AddFinding(new Finding(null, template.Rule, volume, sourceRoot, targetPath, template.Filter,
                measurement.Bytes, measurement.FileCount));
        }

        private static IEnumerable<string> GetProfiles(string root, ScanSession session)
        {
            string usersRoot = Path.Combine(root, "Users");
            if (!Directory.Exists(usersRoot)) return new string[0];

            try
            {
                return Directory.GetDirectories(usersRoot)
                    .Where(path => !ExcludedProfiles.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                    .Where(path => !PathSafetyPolicy.ContainsReparsePoint(path))
                    .ToArray();
            }
            catch (Exception ex)
            {
                session.AddIssue(usersRoot, "Nao foi possivel listar perfis: " + ex.Message);
                return new string[0];
            }
        }

        private static string GetProfileBase(string profile, WindowsPathBase pathBase)
        {
            if (pathBase == WindowsPathBase.RoamingAppData) return Path.Combine(profile, "AppData", "Roaming");
            if (pathBase == WindowsPathBase.UserProfile) return profile;
            return Path.Combine(profile, "AppData", "Local");
        }

        private static void Report(Action<string> progress, string message)
        {
            if (progress != null) progress(message);
        }
    }

    public sealed class MeasurementResult
    {
        public MeasurementResult()
        {
            Issues = new List<string>();
        }

        public long FileCount { get; set; }
        public long Bytes { get; set; }
        public bool WasCancelled { get; set; }
        public List<string> Issues { get; private set; }
    }

    public static class FileMeasurement
    {
        public static MeasurementResult MeasureDirectory(string path, CancellationToken cancellationToken)
        {
            return MeasureDirectory(path, cancellationToken, 0);
        }

        public static MeasurementResult MeasureDirectory(string path, CancellationToken cancellationToken, int minimumAgeDays)
        {
            MeasurementResult result = new MeasurementResult();
            Stack<string> directories = new Stack<string>();
            directories.Push(path);

            while (directories.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    return result;
                }

                string current = directories.Pop();
                if (!Directory.Exists(current)) continue;
                if (PathSafetyPolicy.ContainsReparsePoint(current))
                {
                    result.Issues.Add("Link ou junction ignorado em " + current);
                    continue;
                }

                try
                {
                    foreach (string item in Directory.GetFileSystemEntries(current))
                    {
                        if (!File.Exists(item) && !Directory.Exists(item)) continue;
                        if (PathSafetyPolicy.ContainsReparsePoint(item))
                        {
                            result.Issues.Add("Link ou junction ignorado em " + item);
                            continue;
                        }
                        if (Directory.Exists(item))
                        {
                            directories.Push(item);
                        }
                        else if (File.Exists(item))
                        {
                            try
                            {
                                FileInfo info = new FileInfo(item);
                                if (!IsOldEnough(info, minimumAgeDays)) continue;
                                result.FileCount++;
                                result.Bytes += info.Length;
                            }
                            catch (Exception ex)
                            {
                                result.Issues.Add("Nao foi possivel medir " + item + ": " + ex.Message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Issues.Add("Nao foi possivel enumerar " + current + ": " + ex.Message);
                }
            }

            return result;
        }

        public static MeasurementResult MeasureMatchingFiles(string path, string filter, CancellationToken cancellationToken)
        {
            return MeasureMatchingFiles(path, filter, cancellationToken, 0);
        }

        public static MeasurementResult MeasureMatchingFiles(string path, string filter, CancellationToken cancellationToken, int minimumAgeDays)
        {
            MeasurementResult result = new MeasurementResult();
            if (cancellationToken.IsCancellationRequested)
            {
                result.WasCancelled = true;
                return result;
            }

            try
            {
                foreach (string file in Directory.GetFiles(path, filter, SearchOption.TopDirectoryOnly))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.WasCancelled = true;
                        return result;
                    }
                    if (!File.Exists(file)) continue;
                    if (PathSafetyPolicy.ContainsReparsePoint(file))
                    {
                        result.Issues.Add("Link ou junction ignorado em " + file);
                        continue;
                    }
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        if (!IsOldEnough(info, minimumAgeDays)) continue;
                        result.FileCount++;
                        result.Bytes += info.Length;
                    }
                    catch (Exception ex)
                    {
                        result.Issues.Add("Nao foi possivel medir " + file + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add("Nao foi possivel enumerar " + path + ": " + ex.Message);
            }

            return result;
        }

        private static bool IsOldEnough(FileInfo info, int minimumAgeDays)
        {
            if (minimumAgeDays <= 0) return true;
            DateTime cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(minimumAgeDays));
            return info.LastWriteTimeUtc <= cutoff;
        }
    }
}
