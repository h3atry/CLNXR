using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Clnxr.Application;
using Clnxr.Core;
using Clnxr.Platform.Windows;

namespace Clnxr.Cli
{
    internal static class Program
    {
        private const int ExitOk = 0;
        private const int ExitUsage = 2;
        private const int ExitScanFailed = 3;
        private const int ExitCleanupFailed = 4;

        private static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (UsageException ex)
            {
                Console.Error.WriteLine("ERRO: " + ex.Message);
                PrintUsage();
                return ExitUsage;
            }

            if (options.Help)
            {
                PrintUsage();
                return ExitOk;
            }

            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                ConsoleCancelEventHandler cancelHandler = delegate(object sender, ConsoleCancelEventArgs eventArgs)
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                    Console.Error.WriteLine("Cancelamento solicitado; aguardando o worker encerrar...");
                };
                Console.CancelKeyPress += cancelHandler;
                try
                {
                    return Execute(options, cancellation.Token);
                }
                finally
                {
                    Console.CancelKeyPress -= cancelHandler;
                }
            }
        }

        private static int Execute(Options options, CancellationToken cancellationToken)
        {
            CleanerApplicationService application = new CleanerApplicationService();
            ScanSession session;
            try
            {
                session = application.Analyze(options.Profile, options.RuleIds, cancellationToken, delegate(string message)
                {
                    if (!options.Quiet) Console.Error.WriteLine("SCAN: " + message);
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Falha na análise: " + ex.Message);
                return ExitScanFailed;
            }

            IList<Finding> selected = SelectFindings(session, options);
            Dictionary<string, object> report = BuildScanReport(session, selected, options);

            if (!options.Confirm)
            {
                report["mode"] = "dry-run";
                report["message"] = "Nenhum arquivo foi alterado. Use --yes para executar uma limpeza explicitamente selecionada.";
                WriteReport(report, options.JsonPath);
                return session.State == SessionState.Failed ? ExitScanFailed : ExitOk;
            }

            if (selected.Count == 0)
            {
                report["mode"] = "clean";
                report["message"] = "Nenhum achado SAFE/REVIEW/ADVANCED autorizado foi selecionado; nada foi alterado.";
                WriteReport(report, options.JsonPath);
                return ExitOk;
            }

            if (selected.Any(finding => finding.Rule.Risk == RiskLevel.Blocked))
            {
                Console.Error.WriteLine("Limpeza recusada: regras BLOCKED não podem entrar no plano.");
                return ExitCleanupFailed;
            }

            CleanupExecution execution;
            try
            {
                execution = application.Clean(session, selected.Select(finding => finding.FindingId), cancellationToken,
                    delegate(CleanupProgress progress)
                    {
                        if (!options.Quiet) Console.Error.WriteLine(string.Format("CLEAN: {0}/{1} {2}", progress.CompletedFindings, progress.TotalFindings, progress.Category));
                    });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Falha na limpeza: " + ex.Message);
                return ExitCleanupFailed;
            }

            report["mode"] = "clean";
            report["receiptPath"] = PathRedactor.Redact(execution.ReceiptPath);
            report["receipt"] = BuildReceiptReport(execution.Receipt);
            report["message"] = execution.Receipt.WasCancelled
                ? "Limpeza cancelada; o recibo parcial foi salvo localmente."
                : "Limpeza concluída; o recibo verificável foi salvo localmente.";
            WriteReport(report, options.JsonPath);
            return execution.Receipt.WasCancelled ? ExitCleanupFailed : ExitOk;
        }

        private static IList<Finding> SelectFindings(ScanSession session, Options options)
        {
            IEnumerable<Finding> candidates = session.Findings;
            if (options.RuleIds.Count > 0)
                candidates = candidates.Where(finding => options.RuleIds.Contains(finding.Rule.RuleId));
            else
                candidates = candidates.Where(finding => finding.Rule.Risk == RiskLevel.Safe);

            candidates = candidates.Where(finding => finding.Rule.Risk != RiskLevel.Blocked);
            if (!options.AllowReview)
                candidates = candidates.Where(finding => finding.Rule.Risk != RiskLevel.Review);
            if (!options.AllowAdvanced)
                candidates = candidates.Where(finding => finding.Rule.Risk != RiskLevel.Advanced);
            return candidates.ToList();
        }

        private static Dictionary<string, object> BuildScanReport(ScanSession session, IList<Finding> selected, Options options)
        {
            return new Dictionary<string, object>
            {
                { "schemaVersion", "clnxr.cli.report.v1" },
                { "profile", session.ProfileName },
                { "catalogVersion", session.CatalogVersion },
                { "sessionId", session.SessionId },
                { "state", session.State.ToString() },
                { "startedUtc", session.StartedUtc.ToString("o") },
                { "findingCount", session.Findings.Count },
                { "selectedCount", selected.Count },
                { "estimatedFiles", selected.Sum(finding => finding.FileCount) },
                { "estimatedBytes", selected.Sum(finding => finding.EstimatedBytes) },
                { "findings", session.Findings.Select(BuildFindingReport).ToList() },
                { "issues", session.Issues.Select(issue => new Dictionary<string, object>
                    {
                        { "scope", PathRedactor.Redact(issue.Scope) },
                        { "message", PathRedactor.Redact(issue.Message) }
                    }).ToList() },
                { "selectionPolicy", options.RuleIds.Count > 0 ? "explicit-rule-ids" : "safe-only" }
            };
        }

        private static Dictionary<string, object> BuildFindingReport(Finding finding)
        {
            return new Dictionary<string, object>
            {
                { "findingId", finding.FindingId },
                { "ruleId", finding.Rule.RuleId },
                { "ruleVersion", finding.Rule.Version },
                { "risk", finding.Rule.Risk.ToString().ToUpperInvariant() },
                { "category", finding.Rule.Category },
                { "explanation", finding.Rule.Explanation },
                { "requiredClosedProcesses", finding.Rule.RequiredClosedProcesses.ToArray() },
                { "minimumAgeDays", finding.Rule.MinimumAgeDays },
                { "volume", PathRedactor.Redact(finding.Volume) },
                { "targetPath", PathRedactor.Redact(finding.TargetPath) },
                { "filter", finding.Filter },
                { "fileCount", finding.FileCount },
                { "estimatedBytes", finding.EstimatedBytes }
            };
        }

        private static Dictionary<string, object> BuildReceiptReport(CleanupReceipt receipt)
        {
            return new Dictionary<string, object>
            {
                { "receiptId", receipt.ReceiptId },
                { "schemaVersion", receipt.SchemaVersion },
                { "wasCancelled", receipt.WasCancelled },
                { "filesRemoved", receipt.TotalFilesRemoved },
                { "bytesRemoved", receipt.TotalBytesRemoved },
                { "itemsSkipped", receipt.TotalItemsSkipped },
                { "results", receipt.Results.Select(result => new Dictionary<string, object>
                    {
                        { "findingId", result.FindingId },
                        { "ruleId", result.RuleId },
                        { "category", result.Category },
                        { "targetPath", PathRedactor.Redact(result.TargetPath) },
                        { "status", result.Status.ToString() },
                        { "filesRemoved", result.FilesRemoved },
                        { "bytesRemoved", result.BytesRemoved },
                        { "itemsSkipped", result.ItemsSkipped },
                        { "messages", result.Messages.Select(PathRedactor.Redact).ToArray() }
                    }).ToList() }
            };
        }

        private static void WriteReport(Dictionary<string, object> report, string jsonPath)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            string json = serializer.Serialize(report);
            if (string.IsNullOrWhiteSpace(jsonPath) || string.Equals(jsonPath, "-", StringComparison.Ordinal))
            {
                Console.WriteLine(json);
                return;
            }

            string fullPath = Path.GetFullPath(jsonPath);
            string parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            File.WriteAllText(fullPath, json, new UTF8Encoding(false));
            Console.Error.WriteLine("JSON salvo em " + PathRedactor.Redact(fullPath));
        }

        private static void PrintUsage()
        {
            Console.WriteLine("CLNXR.Cli — modo local, dry-run por padrão");
            Console.WriteLine();
            Console.WriteLine("Uso:");
            Console.WriteLine("  CLNXR.Cli.exe [--profile safe|complete|gaming|developer|personalized]");
            Console.WriteLine("                 [--rules id1,id2] [--allow-review] [--allow-advanced]");
            Console.WriteLine("                 [--clean --yes] [--json caminho|-] [--quiet]");
            Console.WriteLine();
            Console.WriteLine("Sem --yes, o comando apenas analisa e produz um relatório JSON; nenhum arquivo é alterado.");
            Console.WriteLine("--clean sem --yes também permanece em dry-run, para revisão por scripts.");
            Console.WriteLine("--rules exige IDs catalogados; regras BLOCKED nunca são aceitas.");
            Console.WriteLine("Códigos: 0 sucesso, 2 uso inválido, 3 falha/cancelamento de análise, 4 falha/cancelamento de limpeza.");
        }

        private sealed class Options
        {
            public Options()
            {
                Profile = ScanProfile.Safe;
                RuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public bool Help { get; private set; }
            public bool Confirm { get; private set; }
            public bool AllowReview { get; private set; }
            public bool AllowAdvanced { get; private set; }
            public bool Quiet { get; private set; }
            public ScanProfile Profile { get; private set; }
            public HashSet<string> RuleIds { get; private set; }
            public string JsonPath { get; private set; }

            public static Options Parse(string[] args)
            {
                Options options = new Options();
                for (int index = 0; index < (args == null ? 0 : args.Length); index++)
                {
                    string arg = args[index] ?? string.Empty;
                    if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Help = true;
                    }
                    else if (string.Equals(arg, "--yes", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Confirm = true;
                    }
                    else if (string.Equals(arg, "--clean", StringComparison.OrdinalIgnoreCase))
                    {
                        // --clean is intentionally descriptive; --yes is the destructive confirmation.
                    }
                    else if (string.Equals(arg, "--allow-review", StringComparison.OrdinalIgnoreCase))
                    {
                        options.AllowReview = true;
                    }
                    else if (string.Equals(arg, "--allow-advanced", StringComparison.OrdinalIgnoreCase))
                    {
                        options.AllowAdvanced = true;
                    }
                    else if (string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Quiet = true;
                    }
                    else if (string.Equals(arg, "--profile", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Profile = ParseProfile(RequireValue(args, ref index, arg));
                    }
                    else if (string.Equals(arg, "--rules", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = RequireValue(args, ref index, arg);
                        foreach (string ruleId in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string normalized = ruleId.Trim();
                            if (normalized.Length == 0) continue;
                            options.RuleIds.Add(normalized);
                        }
                        if (options.RuleIds.Count == 0) throw new UsageException("--rules precisa conter ao menos um ID.");
                    }
                    else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
                    {
                        options.JsonPath = RequireValue(args, ref index, arg);
                    }
                    else if (arg.Length > 0)
                    {
                        throw new UsageException("Opção desconhecida: " + arg);
                    }
                }

                if (options.Profile == ScanProfile.Personalized && options.RuleIds.Count == 0)
                    throw new UsageException("O perfil personalized exige --rules com IDs explícitos.");
                if (options.AllowAdvanced && !options.AllowReview)
                    throw new UsageException("--allow-advanced exige também --allow-review para tornar o risco explícito.");
                return options;
            }

            private static string RequireValue(string[] args, ref int index, string option)
            {
                if (args == null || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                    throw new UsageException(option + " exige um valor.");
                index++;
                return args[index];
            }

            private static ScanProfile ParseProfile(string value)
            {
                if (string.Equals(value, "safe", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "seguro", StringComparison.OrdinalIgnoreCase)) return ScanProfile.Safe;
                if (string.Equals(value, "complete", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "completo", StringComparison.OrdinalIgnoreCase)) return ScanProfile.Complete;
                if (string.Equals(value, "gaming", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "jogos", StringComparison.OrdinalIgnoreCase)) return ScanProfile.Gaming;
                if (string.Equals(value, "developer", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "desenvolvedor", StringComparison.OrdinalIgnoreCase)) return ScanProfile.Developer;
                if (string.Equals(value, "personalized", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "personalizado", StringComparison.OrdinalIgnoreCase)) return ScanProfile.Personalized;
                throw new UsageException("Perfil inválido: " + value);
            }
        }

        private sealed class UsageException : Exception
        {
            public UsageException(string message) : base(message) { }
        }
    }
}
