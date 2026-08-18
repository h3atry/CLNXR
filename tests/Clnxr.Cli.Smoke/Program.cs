using System;
using System.Diagnostics;
using System.IO;

namespace Clnxr.Cli.Smoke
{
    internal static class Program
    {
        private static int Main()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string cliPath = Path.Combine(root, "CLNXR.Cli.exe");
            if (!File.Exists(cliPath))
            {
                Console.Error.WriteLine("FAIL: CLNXR.Cli.exe não foi copiado para o diretório do smoke test.");
                return 1;
            }

            string reportPath = Path.Combine(Path.GetTempPath(), "clnxr-cli-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                CommandResult help = Run(cliPath, "--help", 30000);
                Expect(help.ExitCode == 0 && help.Stdout.IndexOf("dry-run", StringComparison.OrdinalIgnoreCase) >= 0,
                    "--help precisa retornar 0 e descrever dry-run.");

                CommandResult invalid = Run(cliPath, "--profile personalized --quiet", 30000);
                Expect(invalid.ExitCode == 2 && invalid.Stderr.IndexOf("--rules", StringComparison.OrdinalIgnoreCase) >= 0,
                    "perfil personalized sem IDs precisa retornar erro de uso 2.");

                CommandResult scan = Run(cliPath, "--quiet --json \"" + reportPath + "\"", 120000);
                Expect(scan.ExitCode == 0, "dry-run CLI precisa concluir com código 0.");
                Expect(File.Exists(reportPath), "dry-run CLI precisa escrever o relatório JSON solicitado.");
                string json = File.ReadAllText(reportPath);
                Expect(json.IndexOf("clnxr.cli.report.v1", StringComparison.Ordinal) >= 0, "relatório CLI precisa declarar o esquema.");
                Expect(json.IndexOf("\"mode\":\"dry-run\"", StringComparison.Ordinal) >= 0, "relatório sem --yes precisa declarar dry-run.");

                Console.WriteLine("PASS: CLI smoke validou ajuda, erro de uso e dry-run JSON sem alterar arquivos.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
            finally
            {
                try { if (File.Exists(reportPath)) File.Delete(reportPath); } catch { }
            }
        }

        private static CommandResult Run(string path, string arguments, int timeoutMilliseconds)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(path, arguments);
            startInfo.WorkingDirectory = Path.GetDirectoryName(path);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            using (Process process = Process.Start(startInfo))
            {
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("CLI não terminou dentro do limite de " + timeoutMilliseconds + " ms.");
                }
                return new CommandResult(process.ExitCode, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
            }
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class CommandResult
        {
            public CommandResult(int exitCode, string stdout, string stderr)
            {
                ExitCode = exitCode;
                Stdout = stdout ?? string.Empty;
                Stderr = stderr ?? string.Empty;
            }

            public int ExitCode { get; private set; }
            public string Stdout { get; private set; }
            public string Stderr { get; private set; }
        }
    }
}
