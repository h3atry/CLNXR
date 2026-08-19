using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Clnxr.Core;
using Clnxr.Safety;

namespace Clnxr.Platform.Windows
{
    public sealed class UserDataCleanupPreview
    {
        public UserDataCleanupPreview(string rootPath, long fileCount, long bytes, IList<string> issues)
        {
            RootPath = PathRedactor.Redact(rootPath);
            FileCount = fileCount;
            Bytes = bytes;
            Issues = new List<string>(issues ?? new List<string>()).AsReadOnly();
        }

        public string RootPath { get; private set; }
        public long FileCount { get; private set; }
        public long Bytes { get; private set; }
        public IList<string> Issues { get; private set; }
    }

    public sealed class UserDataCleanupResult
    {
        public UserDataCleanupResult(string rootPath, long removedFiles, long removedBytes, long skippedFiles,
            bool wasCancelled, IList<string> issues)
        {
            RootPath = PathRedactor.Redact(rootPath);
            RemovedFiles = removedFiles;
            RemovedBytes = removedBytes;
            SkippedFiles = skippedFiles;
            WasCancelled = wasCancelled;
            Issues = new List<string>(issues ?? new List<string>()).AsReadOnly();
        }

        public string RootPath { get; private set; }
        public long RemovedFiles { get; private set; }
        public long RemovedBytes { get; private set; }
        public long SkippedFiles { get; private set; }
        public bool WasCancelled { get; private set; }
        public IList<string> Issues { get; private set; }
    }

    /// <summary>
    /// Remove somente os dados criados pelo CLNXR em sua raiz própria de LocalAppData.
    /// Não recebe caminhos arbitrários, não toca dados do usuário e não remove a raiz.
    /// </summary>
    public sealed class UserDataCleanupService
    {
        private readonly string rootPath;

        public UserDataCleanupService()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CLNXR"))
        {
        }

        public UserDataCleanupService(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("A raiz de dados do CLNXR e obrigatoria.", "rootPath");
            string normalized = PathSafetyPolicy.Normalize(rootPath);
            if (string.Equals(Path.GetPathRoot(normalized), normalized, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(new DirectoryInfo(normalized).Name, "CLNXR", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A limpeza de dados só pode apontar para uma pasta CLNXR dedicada.", "rootPath");
            this.rootPath = normalized;
        }

        public string RootPath { get { return rootPath; } }

        public UserDataCleanupPreview Preview(CancellationToken cancellationToken)
        {
            List<string> issues = new List<string>();
            long files = 0;
            long bytes = 0;
            foreach (string file in EnumerateFiles(issues, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    FileInfo info = new FileInfo(file);
                    if (!info.Exists) continue;
                    files++;
                    bytes += Math.Max(0, info.Length);
                }
                catch (Exception ex)
                {
                    issues.Add(PathRedactor.Redact("Não foi possível medir " + file + ": " + ex.Message));
                }
            }
            return new UserDataCleanupPreview(rootPath, files, bytes, issues);
        }

        public UserDataCleanupResult Execute(CancellationToken cancellationToken)
        {
            List<string> issues = new List<string>();
            long removedFiles = 0;
            long removedBytes = 0;
            long skippedFiles = 0;
            bool cancelled = false;
            List<string> files = EnumerateFiles(issues, cancellationToken).ToList();
            if (cancellationToken.IsCancellationRequested)
                cancelled = true;
            foreach (string file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                try
                {
                    FileInfo info = new FileInfo(file);
                    if (!info.Exists) continue;
                    long size = Math.Max(0, info.Length);
                    File.Delete(file);
                    removedFiles++;
                    removedBytes += size;
                }
                catch (Exception ex)
                {
                    skippedFiles++;
                    issues.Add(PathRedactor.Redact("Não foi possível remover " + file + ": " + ex.Message));
                }
            }

            if (!cancelled) RemoveEmptyDirectories(issues);
            return new UserDataCleanupResult(rootPath, removedFiles, removedBytes, skippedFiles, cancelled, issues);
        }

        private IEnumerable<string> EnumerateFiles(ICollection<string> issues, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(rootPath)) yield break;
            if (PathSafetyPolicy.ContainsReparsePoint(rootPath))
            {
                issues.Add("A raiz de dados do CLNXR é um reparse point e foi preservada.");
                yield break;
            }

            Stack<string> pending = new Stack<string>();
            pending.Push(rootPath);
            while (pending.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                string directory = pending.Pop();
                string[] entries;
                try { entries = Directory.GetFileSystemEntries(directory); }
                catch (Exception ex)
                {
                    issues.Add(PathRedactor.Redact("Não foi possível enumerar " + directory + ": " + ex.Message));
                    continue;
                }

                foreach (string entry in entries)
                {
                    if (cancellationToken.IsCancellationRequested) yield break;
                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(entry);
                    }
                    catch (Exception ex)
                    {
                        issues.Add(PathRedactor.Redact("Não foi possível inspecionar " + entry + ": " + ex.Message));
                        continue;
                    }

                    if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        issues.Add(PathRedactor.Redact("Reparse point preservado: " + entry));
                        continue;
                    }
                    if ((attributes & FileAttributes.Directory) == FileAttributes.Directory) pending.Push(entry);
                    else yield return entry;
                }
            }
        }

        private void RemoveEmptyDirectories(ICollection<string> issues)
        {
            if (!Directory.Exists(rootPath)) return;
            List<string> directories = new List<string>();
            try { directories.AddRange(Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)); }
            catch (Exception ex) { issues.Add(PathRedactor.Redact("Não foi possível enumerar diretórios vazios: " + ex.Message)); return; }
            foreach (string directory in directories.OrderByDescending(item => item.Length))
            {
                try
                {
                    if (!PathSafetyPolicy.ContainsReparsePoint(directory) && Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                        Directory.Delete(directory, false);
                }
                catch (Exception ex) { issues.Add(PathRedactor.Redact("Não foi possível remover diretório vazio " + directory + ": " + ex.Message)); }
            }
        }
    }
}
