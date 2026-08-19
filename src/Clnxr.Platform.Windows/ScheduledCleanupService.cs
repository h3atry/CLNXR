using System;
using System.Diagnostics;
using System.IO;
using Clnxr.Core;
using Clnxr.Safety;

namespace Clnxr.Platform.Windows
{
    public sealed class ScheduledCleanupPlan
    {
        public ScheduledCleanupPlan(string taskName, string executablePath, string arguments, string trigger)
        {
            TaskName = taskName ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            Arguments = arguments ?? string.Empty;
            Trigger = trigger ?? string.Empty;
        }

        public string TaskName { get; private set; }
        public string ExecutablePath { get; private set; }
        public string Arguments { get; private set; }
        public string Trigger { get; private set; }
    }

    public sealed class ScheduledCleanupResult
    {
        public ScheduledCleanupResult(bool succeeded, string message, string command)
        {
            Succeeded = succeeded;
            Message = PathRedactor.Redact(message);
            Command = PathRedactor.Redact(command);
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public string Command { get; private set; }
    }

    /// <summary>
    /// Agendamento explícito e reversível somente do perfil Seguro. O comando
    /// é fixo, não aceita shell arbitrário e usa schtasks.exe sem elevação
    /// automática. Criar/remover a tarefa só ocorre quando o usuário confirma.
    /// </summary>
    public sealed class ScheduledCleanupService
    {
        public const string TaskName = "CLNXR Safe Daily Cleanup";
        private const string SafeArguments = "--profile safe --clean --yes --quiet";

        public ScheduledCleanupPlan BuildSafeDailyPlan(string executablePath)
        {
            string normalized = PathSafetyPolicy.Normalize(executablePath);
            if (!File.Exists(normalized)) throw new FileNotFoundException("O executável portátil não existe.", normalized);
            if (!string.Equals(Path.GetExtension(normalized), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("O agendamento exige um executável .exe.");
            if (PathSafetyPolicy.ContainsReparsePoint(normalized))
                throw new InvalidOperationException("O executável não pode estar em um reparse point.");

            return new ScheduledCleanupPlan(TaskName, normalized, SafeArguments, "Diário às 03:00");
        }

        public string BuildCreateArguments(ScheduledCleanupPlan plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            if (!string.Equals(plan.TaskName, TaskName, StringComparison.Ordinal))
                throw new InvalidOperationException("Somente a tarefa fixa do CLNXR pode ser agendada.");
            if (!string.Equals(plan.Arguments, SafeArguments, StringComparison.Ordinal))
                throw new InvalidOperationException("Os argumentos do agendamento Seguro não podem ser alterados.");
            return "/Create /TN " + Quote(plan.TaskName) + " /SC DAILY /ST 03:00 /RL LIMITED /F /TR " +
                QuoteTaskCommand(Quote(plan.ExecutablePath) + " " + plan.Arguments);
        }

        public string BuildDeleteArguments()
        {
            return "/Delete /TN " + Quote(TaskName) + " /F";
        }

        public ScheduledCleanupResult Create(ScheduledCleanupPlan plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            return Run(BuildCreateArguments(plan), "agendar a limpeza segura diária");
        }

        public ScheduledCleanupResult Remove()
        {
            return Run(BuildDeleteArguments(), "remover o agendamento da limpeza segura diária");
        }

        private static ScheduledCleanupResult Run(string arguments, string action)
        {
            string executable = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
            if (!File.Exists(executable))
                return new ScheduledCleanupResult(false, "schtasks.exe não está disponível nesta instalação do Windows.", executable + " " + arguments);

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(executable, arguments);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                        return new ScheduledCleanupResult(false, "O Windows não iniciou o comando para " + action + ".", executable + " " + arguments);
                    if (!process.WaitForExit(20000))
                    {
                        try { process.Kill(); } catch { }
                        return new ScheduledCleanupResult(false, "O comando para " + action + " excedeu o limite de 20 segundos.", executable + " " + arguments);
                    }

                    string standardError = process.StandardError.ReadToEnd();
                    string standardOutput = process.StandardOutput.ReadToEnd();
                    if (process.ExitCode != 0)
                    {
                        string detail = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
                        return new ScheduledCleanupResult(false, "O Windows recusou " + action + ": " + detail.Trim(), executable + " " + arguments);
                    }
                    return new ScheduledCleanupResult(true, "O Windows concluiu " + action + " sem elevação automática.", executable + " " + arguments);
                }
            }
            catch (Exception ex)
            {
                return new ScheduledCleanupResult(false, "Falha ao " + action + ": " + ex.Message, executable + " " + arguments);
            }
        }

        private static string Quote(string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (value.IndexOf('"') >= 0) throw new ArgumentException("O valor contém aspas inválidas.", "value");
            return "\"" + value + "\"";
        }

        private static string QuoteTaskCommand(string command)
        {
            if (command == null) throw new ArgumentNullException("command");
            return "\"" + command.Replace("\"", "\\\"") + "\"";
        }
    }
}
