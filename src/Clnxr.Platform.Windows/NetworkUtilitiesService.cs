using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Clnxr.Core;

namespace Clnxr.Platform.Windows
{
    public sealed class NetworkActionPlan
    {
        public NetworkActionPlan(string actionId, string title, string executablePath, string arguments,
            string description, bool readOnly, bool requiresElevation, bool requiresRestart)
        {
            ActionId = actionId ?? string.Empty;
            Title = title ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            Arguments = arguments ?? string.Empty;
            Description = description ?? string.Empty;
            ReadOnly = readOnly;
            RequiresElevation = requiresElevation;
            RequiresRestart = requiresRestart;
        }

        public string ActionId { get; private set; }
        public string Title { get; private set; }
        public string ExecutablePath { get; private set; }
        public string Arguments { get; private set; }
        public string Description { get; private set; }
        public bool ReadOnly { get; private set; }
        public bool RequiresElevation { get; private set; }
        public bool RequiresRestart { get; private set; }
    }

    public sealed class NetworkDiagnosticResult
    {
        public NetworkDiagnosticResult(bool succeeded, string message, string output, IList<string> issues, string command)
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

    public sealed class NetworkActionResult
    {
        public NetworkActionResult(bool succeeded, string message, string output, IList<string> issues, string command)
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
    /// Utilitários de rede com catálogo fechado. Diagnóstico é somente leitura;
    /// Flush DNS exige confirmação. Reset de Winsock/IP fica documentado como
    /// plano manual porque é disruptivo, exige elevação e reinicialização.
    /// </summary>
    public sealed class NetworkUtilitiesService
    {
        public const string DiagnosticsActionId = "network-diagnostics";
        public const string FlushDnsActionId = "flush-dns";
        public const string WinsockResetActionId = "winsock-reset";
        public const string IpResetActionId = "ip-reset";

        private const int TimeoutMilliseconds = 20000;
        private readonly string ipconfigPath;
        private readonly string netshPath;

        public NetworkUtilitiesService()
        {
            ipconfigPath = Path.Combine(Environment.SystemDirectory, "ipconfig.exe");
            netshPath = Path.Combine(Environment.SystemDirectory, "netsh.exe");
        }

        public IList<NetworkActionPlan> ListPlans()
        {
            return new List<NetworkActionPlan>
            {
                BuildPlan(DiagnosticsActionId),
                BuildPlan(FlushDnsActionId),
                BuildPlan(WinsockResetActionId),
                BuildPlan(IpResetActionId)
            };
        }

        public NetworkActionPlan BuildPlan(string actionId)
        {
            if (string.Equals(actionId, DiagnosticsActionId, StringComparison.OrdinalIgnoreCase))
                return new NetworkActionPlan(DiagnosticsActionId, "Diagnóstico de rede", ipconfigPath, "/all",
                    "Coleta adaptadores e configuração local sem alterar a rede.", true, false, false);
            if (string.Equals(actionId, FlushDnsActionId, StringComparison.OrdinalIgnoreCase))
                return new NetworkActionPlan(FlushDnsActionId, "Limpar cache DNS", ipconfigPath, "/flushdns",
                    "Limpa somente o cache DNS local; conexões ativas não são reiniciadas pelo CLNXR.", false, false, false);
            if (string.Equals(actionId, WinsockResetActionId, StringComparison.OrdinalIgnoreCase))
                return new NetworkActionPlan(WinsockResetActionId, "Redefinir Winsock", netshPath, "winsock reset",
                    "Plano manual: altera a pilha Winsock, exige administrador e reinicialização.", false, true, true);
            if (string.Equals(actionId, IpResetActionId, StringComparison.OrdinalIgnoreCase))
                return new NetworkActionPlan(IpResetActionId, "Redefinir TCP/IP", netshPath, "int ip reset",
                    "Plano manual: altera a pilha TCP/IP, exige administrador e pode exigir reinicialização.", false, true, true);

            throw new ArgumentException("Ação de rede não pertence ao catálogo fechado do CLNXR.", "actionId");
        }

        public NetworkDiagnosticResult Diagnose()
        {
            NetworkActionPlan plan = BuildPlan(DiagnosticsActionId);
            if (!File.Exists(plan.ExecutablePath))
                return new NetworkDiagnosticResult(false, "ipconfig.exe não está disponível nesta instalação do Windows.", string.Empty, new[] { "Executável ausente." }, BuildCommand(plan));

            CommandResult command = Run(plan);
            return new NetworkDiagnosticResult(command.ExitCode == 0,
                command.ExitCode == 0 ? "Diagnóstico de rede concluído em modo somente leitura." : "O diagnóstico de rede terminou com erro.",
                command.Output, command.Issues, BuildCommand(plan));
        }

        public NetworkActionResult Execute(NetworkActionPlan plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            NetworkActionPlan canonical = BuildPlan(plan.ActionId);
            if (!string.Equals(canonical.Arguments, plan.Arguments, StringComparison.Ordinal) ||
                !string.Equals(canonical.ExecutablePath, plan.ExecutablePath, StringComparison.Ordinal))
                throw new InvalidOperationException("O plano de rede não corresponde ao catálogo fechado.");

            if (canonical.ReadOnly)
            {
                NetworkDiagnosticResult diagnostic = Diagnose();
                return new NetworkActionResult(diagnostic.Succeeded, diagnostic.Message, diagnostic.Output, diagnostic.Issues, diagnostic.Command);
            }

            if (string.Equals(canonical.ActionId, WinsockResetActionId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(canonical.ActionId, IpResetActionId, StringComparison.OrdinalIgnoreCase))
            {
                return new NetworkActionResult(false,
                    "Este reset é apenas um plano manual: exige execução elevada e reinicialização. O CLNXR não solicita elevação automática.",
                    string.Empty, new[] { "Nenhuma alteração foi executada." }, BuildCommand(canonical));
            }

            if (!File.Exists(canonical.ExecutablePath))
                return new NetworkActionResult(false, "ipconfig.exe não está disponível nesta instalação do Windows.", string.Empty, new[] { "Executável ausente." }, BuildCommand(canonical));

            CommandResult command = Run(canonical);
            return new NetworkActionResult(command.ExitCode == 0,
                command.ExitCode == 0 ? "O cache DNS local foi limpo." : "O Windows recusou a limpeza do cache DNS.",
                command.Output, command.Issues, BuildCommand(canonical));
        }

        private static string BuildCommand(NetworkActionPlan plan)
        {
            return "\"" + plan.ExecutablePath + "\" " + plan.Arguments;
        }

        private static CommandResult Run(NetworkActionPlan plan)
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
                        return new CommandResult(-1, string.Empty, new[] { "O comando excedeu o limite de 20 segundos." });
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
