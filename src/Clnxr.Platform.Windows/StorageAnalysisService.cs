using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Clnxr.Safety;

namespace Clnxr.Platform.Windows
{
    public sealed class StorageAnalysisProgress
    {
        public StorageAnalysisProgress(string stage, string currentPath, long filesVisited)
        {
            Stage = stage ?? string.Empty;
            CurrentPath = currentPath ?? string.Empty;
            FilesVisited = filesVisited;
        }

        public string Stage { get; private set; }
        public string CurrentPath { get; private set; }
        public long FilesVisited { get; private set; }
    }

    public sealed class StorageAnalysisIssue
    {
        public StorageAnalysisIssue(string path, string message)
        {
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Path { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class DiskUsageEntry
    {
        public DiskUsageEntry(string volume, string path, long fileCount, long bytes)
        {
            Volume = volume ?? string.Empty;
            Path = path ?? string.Empty;
            FileCount = fileCount;
            Bytes = bytes;
        }

        public string Volume { get; private set; }
        public string Path { get; private set; }
        public long FileCount { get; private set; }
        public long Bytes { get; private set; }
    }

    public sealed class LargeFileCandidate
    {
        public LargeFileCandidate(string volume, string path, long bytes, DateTime modifiedUtc)
        {
            Volume = volume ?? string.Empty;
            Path = path ?? string.Empty;
            Bytes = bytes;
            ModifiedUtc = modifiedUtc;
        }

        public string Volume { get; private set; }
        public string Path { get; private set; }
        public long Bytes { get; private set; }
        public DateTime ModifiedUtc { get; private set; }
    }

    public sealed class DuplicateGroup
    {
        public DuplicateGroup(string hash, long bytesPerFile, IEnumerable<string> paths)
        {
            Hash = hash ?? string.Empty;
            BytesPerFile = bytesPerFile;
            Paths = new List<string>(paths ?? Enumerable.Empty<string>()).AsReadOnly();
        }

        public string Hash { get; private set; }
        public long BytesPerFile { get; private set; }
        public IList<string> Paths { get; private set; }
        public int FileCount { get { return Paths.Count; } }
        public long PotentialRecoverableBytes { get { return FileCount > 1 ? BytesPerFile * (FileCount - 1) : 0; } }
    }

    public sealed class StorageAnalysisResult
    {
        public StorageAnalysisResult()
        {
            DiskEntries = new List<DiskUsageEntry>();
            LargeFiles = new List<LargeFileCandidate>();
            DuplicateGroups = new List<DuplicateGroup>();
            Issues = new List<StorageAnalysisIssue>();
        }

        public bool WasCancelled { get; set; }
        public long FilesVisited { get; set; }
        public IList<DiskUsageEntry> DiskEntries { get; private set; }
        public IList<LargeFileCandidate> LargeFiles { get; private set; }
        public IList<DuplicateGroup> DuplicateGroups { get; private set; }
        public IList<StorageAnalysisIssue> Issues { get; private set; }
    }

    public sealed class StorageAnalysisService
    {
        public StorageAnalysisResult BuildDiskMap(CancellationToken cancellationToken, Action<StorageAnalysisProgress> progress)
        {
            return BuildDiskMap(GetEligibleDriveRoots(), cancellationToken, progress);
        }

        public StorageAnalysisResult BuildDiskMap(IEnumerable<string> roots, CancellationToken cancellationToken, Action<StorageAnalysisProgress> progress)
        {
            StorageAnalysisResult result = new StorageAnalysisResult();
            foreach (string root in NormalizeRoots(roots, result))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    return result;
                }

                string volume = Path.GetPathRoot(root) ?? root;
                string[] children;
                try
                {
                    children = Directory.GetFileSystemEntries(root);
                }
                catch (Exception ex)
                {
                    result.Issues.Add(new StorageAnalysisIssue(root, "Nao foi possivel enumerar a raiz do mapa: " + ex.Message));
                    continue;
                }

                foreach (string child in children)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.WasCancelled = true;
                        return result;
                    }
                    if (PathSafetyPolicy.ContainsReparsePoint(child))
                    {
                        result.Issues.Add(new StorageAnalysisIssue(child, "Link ou junction ignorado durante o mapa somente leitura."));
                        continue;
                    }

                    Report(progress, "Mapeando", child, result.FilesVisited);
                    if (Directory.Exists(child))
                    {
                        MeasurementResult measurement = FileMeasurement.MeasureDirectory(child, cancellationToken);
                        result.FilesVisited += measurement.FileCount;
                        foreach (string issue in measurement.Issues) result.Issues.Add(new StorageAnalysisIssue(child, issue));
                        if (measurement.WasCancelled)
                        {
                            result.WasCancelled = true;
                            return result;
                        }
                        result.DiskEntries.Add(new DiskUsageEntry(volume, child, measurement.FileCount, measurement.Bytes));
                    }
                    else if (File.Exists(child))
                    {
                        try
                        {
                            FileInfo info = new FileInfo(child);
                            result.FilesVisited++;
                            result.DiskEntries.Add(new DiskUsageEntry(volume, child, 1, info.Length));
                        }
                        catch (Exception ex)
                        {
                            result.Issues.Add(new StorageAnalysisIssue(child, "Nao foi possivel medir arquivo da raiz: " + ex.Message));
                        }
                    }
                }
            }

            SortDiskEntries(result.DiskEntries);
            return result;
        }

        public StorageAnalysisResult FindLargeFiles(long minimumBytes, int maximumResults, CancellationToken cancellationToken,
            Action<StorageAnalysisProgress> progress)
        {
            return FindLargeFiles(GetEligibleDriveRoots(), minimumBytes, maximumResults, cancellationToken, progress);
        }

        public StorageAnalysisResult FindLargeFiles(IEnumerable<string> roots, long minimumBytes, int maximumResults,
            CancellationToken cancellationToken, Action<StorageAnalysisProgress> progress)
        {
            if (minimumBytes < 0) throw new ArgumentOutOfRangeException("minimumBytes");
            if (maximumResults <= 0) throw new ArgumentOutOfRangeException("maximumResults");

            StorageAnalysisResult result = new StorageAnalysisResult();
            List<StorageFile> files = CollectFiles(roots, minimumBytes, 200000, "Procurando arquivos grandes", result, cancellationToken, progress);
            if (result.WasCancelled) return result;

            foreach (StorageFile file in files.OrderByDescending(item => item.Bytes).ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase).Take(maximumResults))
                result.LargeFiles.Add(new LargeFileCandidate(file.Volume, file.Path, file.Bytes, file.ModifiedUtc));

            if (files.Count > maximumResults)
                result.Issues.Add(new StorageAnalysisIssue(string.Empty, "A lista foi limitada a " + maximumResults + " resultado(s); a analise permaneceu somente leitura."));
            return result;
        }

        public StorageAnalysisResult FindDuplicates(long minimumBytes, int maximumFilesToHash, CancellationToken cancellationToken,
            Action<StorageAnalysisProgress> progress)
        {
            return FindDuplicates(GetEligibleDriveRoots(), minimumBytes, maximumFilesToHash, cancellationToken, progress);
        }

        public StorageAnalysisResult FindDuplicates(IEnumerable<string> roots, long minimumBytes, int maximumFilesToHash,
            CancellationToken cancellationToken, Action<StorageAnalysisProgress> progress)
        {
            if (minimumBytes < 0) throw new ArgumentOutOfRangeException("minimumBytes");
            if (maximumFilesToHash <= 0) throw new ArgumentOutOfRangeException("maximumFilesToHash");

            StorageAnalysisResult result = new StorageAnalysisResult();
            List<StorageFile> candidates = CollectFiles(roots, minimumBytes, maximumFilesToHash, "Preparando comparacao de duplicados", result, cancellationToken, progress);
            if (result.WasCancelled) return result;

            int hashed = 0;
            foreach (IGrouping<long, StorageFile> sameSize in candidates.GroupBy(file => file.Bytes).Where(group => group.Count() > 1))
            {
                Dictionary<string, List<StorageFile>> byHash = new Dictionary<string, List<StorageFile>>(StringComparer.OrdinalIgnoreCase);
                foreach (StorageFile file in sameSize)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.WasCancelled = true;
                        return result;
                    }
                    if (hashed >= maximumFilesToHash)
                    {
                        result.Issues.Add(new StorageAnalysisIssue(string.Empty, "Limite de " + maximumFilesToHash + " arquivos para hash atingido; grupos seguintes nao foram comparados."));
                        return result;
                    }

                    Report(progress, "Comparando hashes", file.Path, result.FilesVisited + hashed);
                    string hash = ComputeHash(file.Path, cancellationToken, result);
                    if (result.WasCancelled) return result;
                    hashed++;
                    if (string.IsNullOrEmpty(hash)) continue;

                    List<StorageFile> group;
                    if (!byHash.TryGetValue(hash, out group))
                    {
                        group = new List<StorageFile>();
                        byHash.Add(hash, group);
                    }
                    group.Add(file);
                }

                foreach (KeyValuePair<string, List<StorageFile>> pair in byHash.Where(item => item.Value.Count > 1))
                    result.DuplicateGroups.Add(new DuplicateGroup(pair.Key, sameSize.Key, pair.Value.Select(file => file.Path)));
            }

            SortDuplicateGroups(result.DuplicateGroups);
            return result;
        }

        private static IEnumerable<string> GetEligibleDriveRoots()
        {
            List<string> roots = new List<string>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                        roots.Add(drive.RootDirectory.FullName);
                }
                catch
                {
                }
            }
            return roots;
        }

        private static IEnumerable<string> NormalizeRoots(IEnumerable<string> roots, StorageAnalysisResult result)
        {
            foreach (string root in roots ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                string normalized;
                try
                {
                    normalized = PathSafetyPolicy.Normalize(root);
                }
                catch (Exception ex)
                {
                    result.Issues.Add(new StorageAnalysisIssue(root, "Raiz invalida: " + ex.Message));
                    continue;
                }
                if (!Directory.Exists(normalized))
                {
                    result.Issues.Add(new StorageAnalysisIssue(normalized, "Raiz inexistente ou indisponivel."));
                    continue;
                }
                if (PathSafetyPolicy.ContainsReparsePoint(normalized))
                {
                    result.Issues.Add(new StorageAnalysisIssue(normalized, "Raiz com link ou junction foi ignorada."));
                    continue;
                }
                yield return normalized;
            }
        }

        private static List<StorageFile> CollectFiles(IEnumerable<string> roots, long minimumBytes, int maximumCandidates, string stage,
            StorageAnalysisResult result, CancellationToken cancellationToken, Action<StorageAnalysisProgress> progress)
        {
            List<StorageFile> files = new List<StorageFile>();
            foreach (string root in NormalizeRoots(roots, result))
            {
                Stack<string> directories = new Stack<string>();
                directories.Push(root);
                string volume = Path.GetPathRoot(root) ?? root;

                while (directories.Count > 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.WasCancelled = true;
                        return files;
                    }

                    string directory = directories.Pop();
                    if (PathSafetyPolicy.ContainsReparsePoint(directory))
                    {
                        result.Issues.Add(new StorageAnalysisIssue(directory, "Link ou junction ignorado durante a analise somente leitura."));
                        continue;
                    }

                    string[] entries;
                    try
                    {
                        entries = Directory.GetFileSystemEntries(directory);
                    }
                    catch (Exception ex)
                    {
                        result.Issues.Add(new StorageAnalysisIssue(directory, "Nao foi possivel enumerar: " + ex.Message));
                        continue;
                    }

                    foreach (string entry in entries)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            result.WasCancelled = true;
                            return files;
                        }
                        if (PathSafetyPolicy.ContainsReparsePoint(entry))
                        {
                            result.Issues.Add(new StorageAnalysisIssue(entry, "Link ou junction ignorado durante a analise somente leitura."));
                            continue;
                        }
                        if (Directory.Exists(entry))
                        {
                            directories.Push(entry);
                            continue;
                        }
                        if (!File.Exists(entry)) continue;

                        try
                        {
                            FileInfo info = new FileInfo(entry);
                            result.FilesVisited++;
                            if (result.FilesVisited % 64 == 0) Report(progress, stage, entry, result.FilesVisited);
                            if (info.Length < minimumBytes) continue;
                            if (files.Count >= maximumCandidates)
                            {
                                result.Issues.Add(new StorageAnalysisIssue(entry, "Limite de candidatos atingido; a analise foi interrompida sem alterar arquivos."));
                                return files;
                            }
                            files.Add(new StorageFile(volume, entry, info.Length, info.LastWriteTimeUtc));
                        }
                        catch (Exception ex)
                        {
                            result.Issues.Add(new StorageAnalysisIssue(entry, "Nao foi possivel ler metadados: " + ex.Message));
                        }
                    }
                }
            }
            return files;
        }

        private static string ComputeHash(string path, CancellationToken cancellationToken, StorageAnalysisResult result)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (SHA256 hash = SHA256.Create())
                {
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            result.WasCancelled = true;
                            return string.Empty;
                        }
                        hash.TransformBlock(buffer, 0, read, buffer, 0);
                    }
                    hash.TransformFinalBlock(new byte[0], 0, 0);
                    return BitConverter.ToString(hash.Hash).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(new StorageAnalysisIssue(path, "Nao foi possivel calcular hash: " + ex.Message));
                return string.Empty;
            }
        }

        private static void SortDiskEntries(IList<DiskUsageEntry> entries)
        {
            List<DiskUsageEntry> sorted = entries.OrderByDescending(entry => entry.Bytes).ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList();
            entries.Clear();
            foreach (DiskUsageEntry entry in sorted) entries.Add(entry);
        }

        private static void SortDuplicateGroups(IList<DuplicateGroup> groups)
        {
            List<DuplicateGroup> sorted = groups.OrderByDescending(group => group.PotentialRecoverableBytes)
                .ThenBy(group => group.Hash, StringComparer.OrdinalIgnoreCase).ToList();
            groups.Clear();
            foreach (DuplicateGroup group in sorted) groups.Add(group);
        }

        private static void Report(Action<StorageAnalysisProgress> progress, string stage, string path, long filesVisited)
        {
            if (progress != null) progress(new StorageAnalysisProgress(stage, path, filesVisited));
        }

        private sealed class StorageFile
        {
            public StorageFile(string volume, string path, long bytes, DateTime modifiedUtc)
            {
                Volume = volume;
                Path = path;
                Bytes = bytes;
                ModifiedUtc = modifiedUtc;
            }

            public string Volume { get; private set; }
            public string Path { get; private set; }
            public long Bytes { get; private set; }
            public DateTime ModifiedUtc { get; private set; }
        }
    }
}
