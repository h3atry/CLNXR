using System;
using System.Linq;
using System.Threading;
using Clnxr.Core;
using Clnxr.Platform.Windows;
using Clnxr.Safety;

namespace Clnxr.WindowsScan.Smoke
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                WindowsCandidateScanner scanner = new WindowsCandidateScanner(new PathSafetyPolicy());
                ScanSession session = scanner.Scan(new ScanOptions(ScanProfile.Safe), CancellationToken.None, null);
                if (session.State != SessionState.ReviewReady)
                {
                    Console.Error.WriteLine("FAIL: O smoke test nao concluiu a analise. Estado: " + session.State);
                    return 1;
                }

                Console.WriteLine("PASS: scan real somente leitura concluido.");
                Console.WriteLine("FINDINGS=" + session.Findings.Count);
                Console.WriteLine("FILES=" + session.Findings.Sum(finding => finding.FileCount));
                Console.WriteLine("BYTES=" + session.Findings.Sum(finding => finding.EstimatedBytes));
                Console.WriteLine("ISSUES=" + session.Issues.Count);
                foreach (var group in session.Issues.GroupBy(issue => issue.Scope).OrderBy(group => group.Key))
                    Console.WriteLine("ISSUE_SCOPE=" + group.Key + ";COUNT=" + group.Count());
                foreach (var group in session.Issues.GroupBy(issue => IssueType(issue.Message)).OrderBy(group => group.Key))
                    Console.WriteLine("ISSUE_TYPE=" + group.Key + ";COUNT=" + group.Count());

                RecycleBinSnapshot recycleBin = new RecycleBinService().QueryAllVolumes();
                if (!recycleBin.Available)
                {
                    Console.Error.WriteLine("FAIL: Consulta somente leitura da Lixeira falhou: " + recycleBin.Message);
                    return 1;
                }
                Console.WriteLine("RECYCLE_BIN_ITEMS=" + recycleBin.ItemCount);
                Console.WriteLine("RECYCLE_BIN_BYTES=" + recycleBin.Bytes);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }

        private static string IssueType(string message)
        {
            if (message.StartsWith("Link ou junction ignorado", StringComparison.Ordinal)) return "reparse-point";
            if (message.StartsWith("Nao foi possivel medir", StringComparison.Ordinal)) return "file-measurement";
            if (message.StartsWith("Nao foi possivel enumerar", StringComparison.Ordinal)) return "directory-enumeration";
            return "other";
        }
    }
}
