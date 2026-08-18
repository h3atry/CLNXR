using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Clnxr.Core
{
    public enum RiskLevel
    {
        Safe,
        Review,
        Advanced,
        Blocked
    }

    public enum RuleActionKind
    {
        DirectoryContents,
        MatchingFiles
    }

    public enum SessionState
    {
        Created,
        Scanning,
        ReviewReady,
        Cleaning,
        Completed,
        Cancelled,
        Failed
    }

    public enum ActionStatus
    {
        Removed,
        Skipped,
        Blocked,
        Failed,
        Cancelled
    }

    public enum ToolActionStatus
    {
        Succeeded,
        Failed,
        Cancelled
    }

    public static class ReceiptSchema
    {
        public const string CurrentVersion = "clnxr.receipt.v1";
    }

    public static class PathRedactor
    {
        public static string Redact(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            string normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            int usersIndex = normalized.IndexOf(Path.DirectorySeparatorChar + "Users" + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
            if (usersIndex >= 0)
            {
                int userStart = usersIndex + "\\Users\\".Length;
                int nextSeparator = normalized.IndexOf(Path.DirectorySeparatorChar, userStart);
                if (nextSeparator > userStart)
                    return normalized.Substring(0, userStart) + "<user>" + normalized.Substring(nextSeparator);
            }

            return normalized;
        }
    }

    public sealed class Rule
    {
        public Rule(string ruleId, string version, string category, string explanation, RiskLevel risk,
            RuleActionKind actionKind, IEnumerable<string> profiles, IEnumerable<string> requiredClosedProcesses)
            : this(ruleId, version, category, explanation, risk, actionKind, profiles, requiredClosedProcesses, 0)
        {
        }

        public Rule(string ruleId, string version, string category, string explanation, RiskLevel risk,
            RuleActionKind actionKind, IEnumerable<string> profiles, IEnumerable<string> requiredClosedProcesses,
            int minimumAgeDays)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("ruleId e obrigatorio.", "ruleId");
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("version e obrigatoria.", "version");
            if (minimumAgeDays < 0) throw new ArgumentOutOfRangeException("minimumAgeDays", "A idade minima nao pode ser negativa.");

            RuleId = ruleId;
            Version = version;
            Category = category ?? string.Empty;
            Explanation = explanation ?? string.Empty;
            Risk = risk;
            ActionKind = actionKind;
            Profiles = new ReadOnlyCollection<string>((profiles ?? Enumerable.Empty<string>()).ToList());
            RequiredClosedProcesses = new ReadOnlyCollection<string>((requiredClosedProcesses ?? Enumerable.Empty<string>()).ToList());
            MinimumAgeDays = minimumAgeDays;
        }

        public string RuleId { get; private set; }
        public string Version { get; private set; }
        public string Category { get; private set; }
        public string Explanation { get; private set; }
        public RiskLevel Risk { get; private set; }
        public RuleActionKind ActionKind { get; private set; }
        public ReadOnlyCollection<string> Profiles { get; private set; }
        public ReadOnlyCollection<string> RequiredClosedProcesses { get; private set; }
        public int MinimumAgeDays { get; private set; }
    }

    public sealed class Finding
    {
        public Finding(string findingId, Rule rule, string volume, string sourceRoot, string targetPath, string filter,
            long estimatedBytes, long fileCount)
        {
            if (rule == null) throw new ArgumentNullException("rule");
            FindingId = findingId ?? Guid.NewGuid().ToString("N");
            Rule = rule;
            Volume = volume ?? string.Empty;
            SourceRoot = sourceRoot ?? string.Empty;
            TargetPath = targetPath ?? string.Empty;
            Filter = filter ?? string.Empty;
            EstimatedBytes = estimatedBytes;
            FileCount = fileCount;
        }

        public string FindingId { get; private set; }
        public Rule Rule { get; private set; }
        public string Volume { get; private set; }
        public string SourceRoot { get; private set; }
        public string TargetPath { get; private set; }
        public string Filter { get; private set; }
        public long EstimatedBytes { get; private set; }
        public long FileCount { get; private set; }
        public bool DefaultSelected { get { return Rule.Risk == RiskLevel.Safe; } }
    }

    public sealed class ScanIssue
    {
        public ScanIssue(string scope, string message)
        {
            Scope = scope ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Scope { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class ScanSession
    {
        private readonly List<Finding> findings;
        private readonly List<ScanIssue> issues;

        public ScanSession(string profileName, string catalogVersion)
        {
            SessionId = Guid.NewGuid().ToString("N");
            ProfileName = profileName ?? string.Empty;
            CatalogVersion = catalogVersion ?? string.Empty;
            StartedUtc = DateTime.UtcNow;
            State = SessionState.Created;
            findings = new List<Finding>();
            issues = new List<ScanIssue>();
        }

        public string SessionId { get; private set; }
        public string ProfileName { get; private set; }
        public string CatalogVersion { get; private set; }
        public DateTime StartedUtc { get; private set; }
        public DateTime? CompletedUtc { get; private set; }
        public SessionState State { get; private set; }
        public ReadOnlyCollection<Finding> Findings { get { return new ReadOnlyCollection<Finding>(findings); } }
        public ReadOnlyCollection<ScanIssue> Issues { get { return new ReadOnlyCollection<ScanIssue>(issues); } }

        public void BeginScan()
        {
            EnsureState(SessionState.Created);
            State = SessionState.Scanning;
        }

        public void AddFinding(Finding finding)
        {
            if (finding == null) throw new ArgumentNullException("finding");
            EnsureState(SessionState.Scanning);
            findings.Add(finding);
        }

        public void AddIssue(string scope, string message)
        {
            issues.Add(new ScanIssue(scope, message));
        }

        public void CompleteScan()
        {
            EnsureState(SessionState.Scanning);
            CompletedUtc = DateTime.UtcNow;
            State = SessionState.ReviewReady;
        }

        public void Cancel()
        {
            if (State != SessionState.Completed && State != SessionState.Failed) State = SessionState.Cancelled;
            CompletedUtc = DateTime.UtcNow;
        }

        public void Fail(string scope, string message)
        {
            AddIssue(scope, message);
            State = SessionState.Failed;
            CompletedUtc = DateTime.UtcNow;
        }

        private void EnsureState(SessionState expected)
        {
            if (State != expected)
                throw new InvalidOperationException("Transicao de sessao invalida. Estado atual: " + State + ". Estado exigido: " + expected + ".");
        }
    }

    public sealed class ActionPlan
    {
        private readonly List<Finding> findings;

        private ActionPlan(ScanSession session, IEnumerable<Finding> selectedFindings)
        {
            if (session == null) throw new ArgumentNullException("session");
            if (session.State != SessionState.ReviewReady)
                throw new InvalidOperationException("A sessao precisa estar pronta para revisao antes de criar um plano de limpeza.");

            PlanId = Guid.NewGuid().ToString("N");
            SessionId = session.SessionId;
            CreatedUtc = DateTime.UtcNow;
            findings = (selectedFindings ?? Enumerable.Empty<Finding>()).ToList();
        }

        public string PlanId { get; private set; }
        public string SessionId { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public ReadOnlyCollection<Finding> Findings { get { return new ReadOnlyCollection<Finding>(findings); } }

        public static ActionPlan Create(ScanSession session, IEnumerable<string> selectedFindingIds)
        {
            HashSet<string> selection = new HashSet<string>(selectedFindingIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            List<Finding> findings = session.Findings.Where(f => selection.Contains(f.FindingId)).ToList();

            if (findings.Count == 0)
                throw new InvalidOperationException("O plano de limpeza nao pode ficar vazio.");
            if (findings.Any(f => f.Rule.Risk == RiskLevel.Blocked))
                throw new InvalidOperationException("Uma regra bloqueada nao pode entrar no plano de limpeza.");

            return new ActionPlan(session, findings);
        }
    }

    public sealed class ActionResult
    {
        public ActionResult(Finding finding)
        {
            if (finding == null) throw new ArgumentNullException("finding");
            FindingId = finding.FindingId;
            RuleId = finding.Rule.RuleId;
            Category = finding.Rule.Category;
            TargetPath = PathRedactor.Redact(finding.TargetPath);
            Status = ActionStatus.Skipped;
            Messages = new List<string>();
        }

        public string FindingId { get; private set; }
        public string RuleId { get; private set; }
        public string Category { get; private set; }
        public string TargetPath { get; private set; }
        public ActionStatus Status { get; set; }
        public long FilesRemoved { get; set; }
        public long DirectoriesRemoved { get; set; }
        public long BytesRemoved { get; set; }
        public long ItemsSkipped { get; set; }
        public List<string> Messages { get; private set; }
    }

    public sealed class CleanupReceipt
    {
        public CleanupReceipt(ActionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            SchemaVersion = ReceiptSchema.CurrentVersion;
            ReceiptId = Guid.NewGuid().ToString("N");
            PlanId = plan.PlanId;
            SessionId = plan.SessionId;
            StartedUtc = DateTime.UtcNow;
            Results = new List<ActionResult>();
        }

        public string SchemaVersion { get; private set; }
        public string ReceiptId { get; private set; }
        public string PlanId { get; private set; }
        public string SessionId { get; private set; }
        public DateTime StartedUtc { get; private set; }
        public DateTime CompletedUtc { get; set; }
        public bool WasCancelled { get; set; }
        public string ReceiptHash { get; set; }
        public List<ActionResult> Results { get; private set; }
        public long TotalFilesRemoved { get { return Results.Sum(r => r.FilesRemoved); } }
        public long TotalBytesRemoved { get { return Results.Sum(r => r.BytesRemoved); } }
        public long TotalItemsSkipped { get { return Results.Sum(r => r.ItemsSkipped); } }
        public long TotalFindingsSkipped { get { return Results.Count(r => r.Status == ActionStatus.Skipped || r.Status == ActionStatus.Blocked); } }
        public long TotalSkipped { get { return TotalItemsSkipped; } }
    }

    public sealed class MaintenanceReceipt
    {
        public MaintenanceReceipt(string toolId, long estimatedItems, long estimatedBytes)
        {
            SchemaVersion = ReceiptSchema.CurrentVersion;
            ReceiptId = Guid.NewGuid().ToString("N");
            ToolId = toolId ?? string.Empty;
            StartedUtc = DateTime.UtcNow;
            EstimatedItems = estimatedItems;
            EstimatedBytes = estimatedBytes;
        }

        public string SchemaVersion { get; private set; }
        public string ReceiptId { get; private set; }
        public string ToolId { get; private set; }
        public DateTime StartedUtc { get; private set; }
        public DateTime CompletedUtc { get; set; }
        public ToolActionStatus Status { get; set; }
        public long EstimatedItems { get; private set; }
        public long EstimatedBytes { get; private set; }
        public string Message { get; set; }
        public string ReceiptHash { get; set; }
    }

    public sealed class CleanupProgress
    {
        public CleanupProgress(int completedFindings, int totalFindings, string category)
        {
            CompletedFindings = completedFindings;
            TotalFindings = totalFindings;
            Category = category ?? string.Empty;
        }

        public int CompletedFindings { get; private set; }
        public int TotalFindings { get; private set; }
        public string Category { get; private set; }
    }
}
