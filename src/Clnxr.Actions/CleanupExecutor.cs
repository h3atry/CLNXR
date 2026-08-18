using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Clnxr.Core;
using Clnxr.Safety;

namespace Clnxr.Actions
{
    public interface IProcessInspector
    {
        bool IsAnyRunning(IEnumerable<string> processNames, out string runningProcess);
    }

    public sealed class WindowsProcessInspector : IProcessInspector
    {
        public bool IsAnyRunning(IEnumerable<string> processNames, out string runningProcess)
        {
            runningProcess = string.Empty;
            foreach (string name in processNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                try
                {
                    if (Process.GetProcessesByName(name).Length > 0)
                    {
                        runningProcess = name;
                        return true;
                    }
                }
                catch
                {
                    runningProcess = name;
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class CleanupExecutor
    {
        private readonly PathSafetyPolicy safetyPolicy;
        private readonly IProcessInspector processInspector;

        public CleanupExecutor(PathSafetyPolicy safetyPolicy, IProcessInspector processInspector)
        {
            if (safetyPolicy == null) throw new ArgumentNullException("safetyPolicy");
            if (processInspector == null) throw new ArgumentNullException("processInspector");
            this.safetyPolicy = safetyPolicy;
            this.processInspector = processInspector;
        }

        public CleanupReceipt Execute(ActionPlan plan, CancellationToken cancellationToken, Action<CleanupProgress> progress)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            CleanupReceipt receipt = new CleanupReceipt(plan);

            for (int index = 0; index < plan.Findings.Count; index++)
            {
                Finding finding = plan.Findings[index];
                if (cancellationToken.IsCancellationRequested)
                {
                    receipt.WasCancelled = true;
                    AddCancelledResults(plan.Findings.Skip(index), receipt);
                    break;
                }

                Report(progress, index + 1, plan.Findings.Count, finding.Rule.Category);
                ActionResult result = ExecuteFinding(finding, cancellationToken);
                receipt.Results.Add(result);

                if (result.Status == ActionStatus.Cancelled)
                {
                    receipt.WasCancelled = true;
                    AddCancelledResults(plan.Findings.Skip(index + 1), receipt);
                    break;
                }
            }

            receipt.CompletedUtc = DateTime.UtcNow;
            return receipt;
        }

        private ActionResult ExecuteFinding(Finding finding, CancellationToken cancellationToken)
        {
            ActionResult result = new ActionResult(finding);
            SafetyDecision decision = safetyPolicy.ValidateFinding(finding);
            if (!decision.Allowed)
            {
                result.Status = ActionStatus.Blocked;
                result.ItemsSkipped = Math.Max(finding.FileCount, 1);
                result.Messages.Add(PathRedactor.Redact(decision.Reason));
                return result;
            }

            string runningProcess;
            if (processInspector.IsAnyRunning(finding.Rule.RequiredClosedProcesses, out runningProcess))
            {
                result.Status = ActionStatus.Skipped;
                result.ItemsSkipped = Math.Max(finding.FileCount, 1);
                result.Messages.Add(PathRedactor.Redact("Processo em uso: " + runningProcess + ". O CLNXR nao encerra processos para limpar cache."));
                return result;
            }

            try
            {
                if (finding.Rule.ActionKind == RuleActionKind.DirectoryContents)
                {
                    foreach (string item in Directory.GetFileSystemEntries(decision.CanonicalPath))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            result.Status = ActionStatus.Cancelled;
                            result.Messages.Add("Limpeza cancelada pelo usuario.");
                            return result;
                        }
                        DeleteItem(item, decision.CanonicalPath, result, cancellationToken, finding.Rule.MinimumAgeDays);
                    }
                }
                else
                {
                    foreach (string file in Directory.GetFiles(decision.CanonicalPath, finding.Filter, SearchOption.TopDirectoryOnly))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            result.Status = ActionStatus.Cancelled;
                            result.Messages.Add("Limpeza cancelada pelo usuario.");
                            return result;
                        }
                        DeleteItem(file, decision.CanonicalPath, result, cancellationToken, finding.Rule.MinimumAgeDays);
                    }
                }

                result.Status = result.FilesRemoved > 0 || result.DirectoriesRemoved > 0
                    ? ActionStatus.Removed
                    : ActionStatus.Skipped;
                if (result.Status == ActionStatus.Skipped && result.Messages.Count == 0)
                    result.Messages.Add(PathRedactor.Redact("Nenhum item removivel permaneceu no alvo durante a limpeza."));
            }
            catch (Exception ex)
            {
                result.Status = ActionStatus.Failed;
                result.Messages.Add(PathRedactor.Redact("Falha ao enumerar o alvo: " + ex.Message));
            }

            return result;
        }

        private void DeleteItem(string path, string approvedRoot, ActionResult result, CancellationToken cancellationToken, int minimumAgeDays)
        {
            SafetyDecision decision = safetyPolicy.ValidateExistingItem(path, approvedRoot);
            if (!decision.Allowed)
            {
                result.ItemsSkipped++;
                result.Messages.Add(PathRedactor.Redact("Ignorado: " + decision.Reason));
                return;
            }

            try
            {
                if (Directory.Exists(decision.CanonicalPath))
                {
                    foreach (string child in Directory.GetFileSystemEntries(decision.CanonicalPath))
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        DeleteItem(child, approvedRoot, result, cancellationToken, minimumAgeDays);
                    }

                    if (cancellationToken.IsCancellationRequested) return;
                    if (minimumAgeDays > 0 && new DirectoryInfo(decision.CanonicalPath).LastWriteTimeUtc > DateTime.UtcNow.Subtract(TimeSpan.FromDays(minimumAgeDays)))
                    {
                        result.ItemsSkipped++;
                        result.Messages.Add(PathRedactor.Redact("Diretorio preservado por idade minima da regra."));
                        return;
                    }

                    SafetyDecision deleteDecision = safetyPolicy.ValidateExistingItem(decision.CanonicalPath, approvedRoot);
                    if (!deleteDecision.Allowed)
                    {
                        result.ItemsSkipped++;
                        result.Messages.Add(PathRedactor.Redact("Diretorio ignorado: " + deleteDecision.Reason));
                        return;
                    }
                    Directory.Delete(deleteDecision.CanonicalPath, false);
                    result.DirectoriesRemoved++;
                    return;
                }

                if (File.Exists(decision.CanonicalPath))
                {
                    FileInfo info = new FileInfo(decision.CanonicalPath);
                    long size = info.Length;
                    if (minimumAgeDays > 0 && info.LastWriteTimeUtc > DateTime.UtcNow.Subtract(TimeSpan.FromDays(minimumAgeDays)))
                    {
                        result.ItemsSkipped++;
                        result.Messages.Add(PathRedactor.Redact("Arquivo preservado por idade minima da regra."));
                        return;
                    }

                    SafetyDecision deleteDecision = safetyPolicy.ValidateExistingItem(decision.CanonicalPath, approvedRoot);
                    if (!deleteDecision.Allowed)
                    {
                        result.ItemsSkipped++;
                        result.Messages.Add(PathRedactor.Redact("Arquivo ignorado: " + deleteDecision.Reason));
                        return;
                    }

                    File.Delete(deleteDecision.CanonicalPath);
                    result.FilesRemoved++;
                    result.BytesRemoved += size;
                }
            }
            catch (Exception ex)
            {
                result.ItemsSkipped++;
                result.Messages.Add(PathRedactor.Redact("Ignorado sem forcar exclusao: " + ex.Message));
            }
        }

        private static void AddCancelledResults(IEnumerable<Finding> findings, CleanupReceipt receipt)
        {
            foreach (Finding finding in findings)
            {
                ActionResult result = new ActionResult(finding);
                result.Status = ActionStatus.Cancelled;
                result.Messages.Add(PathRedactor.Redact("Nao iniciado porque a limpeza foi cancelada."));
                receipt.Results.Add(result);
            }
        }

        private static void Report(Action<CleanupProgress> progress, int completed, int total, string category)
        {
            if (progress != null) progress(new CleanupProgress(completed, total, category));
        }
    }
}
