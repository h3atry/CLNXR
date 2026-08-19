using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Clnxr.Core;

namespace Clnxr.Platform.Windows
{
    public sealed class SystemRepairPlan
    {
        public SystemRepairPlan(string actionId, string title, string executablePath, string arguments,
            string description, string volume, bool readOnly, bool requiresElevation)
        {
            ActionId = actionId ?? string.Empty;
            Title = title ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            Arguments = arguments ?? string.Empty;
            Description = description ?? string.Empty;
            Volume = volume ?? string.Empty;
            ReadOnly = readOnly;
            RequiresElevation = requiresElevation;
        }

        public string ActionId { get; private set; }
        public string Title { get; private set; }
        public string ExecutablePath { get; private set; }
        public string Arguments { get; private set; }
        public string Description { get; private set; }
        public string Volume { get; private set; }
        public bool ReadOnly { get; private set; }
        public bool RequiresElevation { get; private set; }
    }

    public sealed class SystemRepairResult
    {
        public SystemRepairResult(bool succeeded, string message, string output, IList<string> issues, string command)
        {
            Succeeded = succeeded;
            Message = PathRedactor.Redact(message);
            Output = PathRedactor.Redact(output);
            Issues = new List<string>(issues ?? new List<string>()).Select(PathRedactor.Redact).ToList();
            Command = PathRedactor.Redact(command);
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public string Output { get; private set; }
        public IList<string> Issues { get; private set; }
        public string Command { get; private set; }
    }

    /// <summary>
    /// Hub de diagnóstico/repair com catálogo fechado. Só verificações e
    /// varreduras não destrutivas são executáveis; /scannow, /RestoreHealth,
    /// /f e outras correções não são disparadas automaticamente.
    /// </summary>
    public sealed class SystemRepairService
    {
        public const string SfcVerifyActionId = "sfc-verifyonly";
        public const string DismCheckHealthActionId = "dism-checkhealth";
        public const string ChkdskScanActionId = "chkdsk-scan";

        private const int TimeoutMilliseconds = 120000;
        private readonly string sfcPath;
        private readonly string dismPath;
        private readonly string chkdskPath;

        public SystemRepairService()
        {
            sfcPath = Path.Combine(Environment.SystemDirectory, "sfc.exe");
            dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");
            chkdskPath = Path.Combine(Environment.SystemDirectory, "chkdsk.exe");
        }

        public IList<SystemRepairPlan> ListPlans()
        {
            string volume = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrWhiteSpace(volume)) volume = volume.TrimEnd('\\');
            return new List<SystemRepairPlan>
            {
                BuildPlan(SfcVerifyActionId, string.Empty),
                BuildPlan(DismCheckHealthActionId, string.Empty),
                BuildPlan(ChkdskScanActionId, volume)
            };
        }

        public SystemRepairPlan BuildPlan(string actionId, string volume)
        {
            if (string.Equals(actionId, SfcVerifyActionId, StringComparison.OrdinalIgnoreCase))
                return new SystemRepairPlan(SfcVerifyActionId, "SFC — verificar integridade", sfcPath, "/verifyonly",
                    "Verifica arquivos protegidos sem tentar corrigir; pode exigir administrador.", string.Empty, true, true);
            if (string.Equals(actionId, DismCheckHealthActionId, StringComparison.OrdinalIgnoreCase))
                return new SystemRepairPlan(DismCheckHealthActionId, "DISM — CheckHealth", dismPath, "/Online /Cleanup-Image /CheckHealth",
                    "Consulta o estado da imagem online sem iniciar reparo; pode exigir administrador.", string.Empty, true, true);
            if (string.Equals(actionId, ChkdskScanActionId, StringComparison.OrdinalIgnoreCase))
            {
                string normalizedVolume = NormalizeVolume(volume);
                return new SystemRepairPlan(ChkdskScanActionId, "CHKDSK — verificar volume", chkdskPath, normalizedVolume + " /scan",
                    "Varre o volume online sem /f ou correção automática; pode consumir disco e exigir administrador.", normalizedVolume, true, true);
            }

            throw new ArgumentException("Ação de reparo não pertence ao catálogo fechado do CLNXR.", "actionId");
        }

        public SystemRepairResult Execute(SystemRepairPlan plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            SystemRepairPlan canonical = BuildPlan(plan.ActionId, plan.Volume);
            if (!string.Equals(canonical.Arguments, plan.Arguments, StringComparison.Ordinal) ||
                !string.Equals(canonical.ExecutablePath, plan.ExecutablePath, StringComparison.Ordinal))
                throw new InvalidOperationException("O plano de reparo não corresponde ao catálogo fechado.");

            if (!File.Exists(canonical.ExecutablePath))
                return new SystemRepairResult(false, "O utilitário do Windows não está disponível nesta instalação.", string.Empty,
                    new[] { "Executável ausente." }, BuildCommand(canonical));

            CommandResult command = Run(canonical);
            return new SystemRepairResult(command.ExitCode == 0,
                command.ExitCode == 0 ? "A verificação do sistema terminou." : "A verificação do sistema terminou com erro ou acesso insuficiente.",
                command.Output, command.Issues, BuildCommand(canonical));
        }

        private static string NormalizeVolume(string volume)
        {
            string value = (volume ?? string.Empty).Trim();
            if (!Regex.IsMatch(value, "^[A-Za-z]:$"))
                throw new ArgumentException("O volume deve ser informado no formato X:, sem switches ou caminho arbitrário.", "volume");
            return value.ToUpperInvariant();
        }

        private static string BuildCommand(SystemRepairPlan plan)
        {
            return "\"" + plan.ExecutablePath + "\" " + plan.Arguments;
        }

        private static CommandResult Run(SystemRepairPlan plan)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(plan.ExecutablePath, plan.Arguments);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) return new CommandResult(-1, string.Empty, new[] { "O Windows não iniciou o comando." });
                    if (!process.WaitForExit(TimeoutMilliseconds))
                    {
                        try { process.Kill(); } catch { }
                        return new CommandResult(-1, string.Empty, new[] { "O comando excedeu o limite de 120 segundos." });
                    }
                    string output = Limit(process.StandardOutput.ReadToEnd());
                    string error = Limit(process.StandardError.ReadToEnd());
                    List<string> issues = new List<string>();
                    if (!string.IsNullOrWhiteSpace(error)) issues.Add(error.Trim());
                    return new CommandResult(process.ExitCode, output, issues);
                }
            }
            catch (Exception ex)
            {
                return new CommandResult(-1, string.Empty, new[] { "Falha ao executar o utilitário: " + ex.Message });
            }
        }

        private static string Limit(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            const int max = 12000;
            return value.Length <= max ? value : value.Substring(0, max) + Environment.NewLine + "[saída truncada pelo limite local]";
        }

        private sealed class CommandResult
        {
            public CommandResult(int exitCode, string output, IList<string> issues)
            {
                ExitCode = exitCode;
                Output = output ?? string.Empty;
                Issues = issues ?? new List<string>();
            }

            public int ExitCode { get; private set; }
            public string Output { get; private set; }
            public IList<string> Issues { get; private set; }
        }
    }
}
