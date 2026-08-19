using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Clnxr.Core;

namespace Clnxr.Safety
{
    public sealed class SafetyDecision
    {
        private SafetyDecision(bool allowed, string canonicalPath, string reason)
        {
            Allowed = allowed;
            CanonicalPath = canonicalPath ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public bool Allowed { get; private set; }
        public string CanonicalPath { get; private set; }
        public string Reason { get; private set; }

        public static SafetyDecision Allow(string canonicalPath)
        {
            return new SafetyDecision(true, canonicalPath, string.Empty);
        }

        public static SafetyDecision Deny(string reason)
        {
            return new SafetyDecision(false, string.Empty, reason);
        }
    }

    public sealed class PathSafetyPolicy
    {
        private static readonly string[] ProtectedDirectoryNames =
        {
            "Desktop", "Documents", "Downloads", "Pictures", "Videos", "Music", "Saved Games"
        };

        private static readonly string[] ProtectedPathFragments =
        {
            "\\AppData\\Local\\Google\\Chrome\\",
            "\\AppData\\Local\\Microsoft\\Edge\\",
            "\\AppData\\Roaming\\Mozilla\\Firefox\\",
            "\\AppData\\Local\\Mozilla\\Firefox\\",
            "\\AppData\\Roaming\\Opera Software\\",
            "\\AppData\\Local\\Opera Software\\",
            "\\AppData\\Local\\BraveSoftware\\",
            "\\Windows\\WinSxS\\",
            "\\Windows\\Installer\\",
            "\\Windows\\servicing\\",
            "\\Windows\\System32\\DriverStore\\"
        };

        private static readonly string[] BrowserCacheRoots =
        {
            "\\AppData\\Local\\Google\\Chrome\\User Data\\",
            "\\AppData\\Local\\Microsoft\\Edge\\User Data\\",
            "\\AppData\\Local\\BraveSoftware\\Brave-Browser\\User Data\\",
            "\\AppData\\Local\\Opera Software\\Opera Stable\\",
            "\\AppData\\Local\\Mozilla\\Firefox\\Profiles\\"
        };

        private static readonly string[] BrowserCacheDirectoryNames =
        {
            "Cache", "Code Cache", "GPUCache", "DawnCache", "ShaderCache", "cache2"
        };

        public SafetyDecision ValidateFinding(Finding finding)
        {
            if (finding == null) return SafetyDecision.Deny("Achado ausente.");
            if (finding.Rule.Risk == RiskLevel.Blocked) return SafetyDecision.Deny("A regra esta bloqueada pela politica do produto.");
            if (string.IsNullOrWhiteSpace(finding.SourceRoot) || string.IsNullOrWhiteSpace(finding.TargetPath))
                return SafetyDecision.Deny("Raiz ou alvo ausente.");

            string sourceRoot;
            string targetPath;
            try
            {
                sourceRoot = Normalize(finding.SourceRoot);
                targetPath = Normalize(finding.TargetPath);
            }
            catch (Exception ex)
            {
                return SafetyDecision.Deny("Caminho invalido: " + ex.Message);
            }

            if (!Directory.Exists(sourceRoot)) return SafetyDecision.Deny("A raiz aprovada nao existe mais.");
            if (!Directory.Exists(targetPath)) return SafetyDecision.Deny("O alvo de diretorio nao existe mais.");
            if (!IsWithinRoot(targetPath, sourceRoot)) return SafetyDecision.Deny("O alvo esta fora da raiz aprovada pela regra.");
            if (IsProtected(targetPath)) return SafetyDecision.Deny("O alvo pertence a uma area permanentemente protegida.");
            if (ContainsReparsePoint(sourceRoot) || ContainsReparsePoint(targetPath))
                return SafetyDecision.Deny("A raiz ou o alvo contem link, junction ou outro reparse point.");

            return SafetyDecision.Allow(targetPath);
        }

        public SafetyDecision ValidateExistingItem(string path, string approvedRoot)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(approvedRoot))
                return SafetyDecision.Deny("Item ou raiz aprovada ausente.");

            string root;
            string target;
            try
            {
                root = Normalize(approvedRoot);
                target = Normalize(path);
            }
            catch (Exception ex)
            {
                return SafetyDecision.Deny("Caminho invalido: " + ex.Message);
            }

            if (!PathExists(target)) return SafetyDecision.Deny("O item nao existe mais.");
            if (!Directory.Exists(root)) return SafetyDecision.Deny("A raiz aprovada nao existe mais.");
            if (!IsWithinRoot(target, root)) return SafetyDecision.Deny("O item saiu da raiz aprovada.");
            if (IsProtected(target)) return SafetyDecision.Deny("O item pertence a uma area permanentemente protegida.");
            if (ContainsReparsePoint(root) || ContainsReparsePoint(target))
                return SafetyDecision.Deny("O item contem link, junction ou outro reparse point.");
            if (File.Exists(target) && HasMultipleHardLinks(target))
                return SafetyDecision.Deny("O item possui hard link adicional e foi preservado por segurança.");

            return SafetyDecision.Allow(target);
        }

        /// <summary>
        /// Verifica o número de nomes físicos de um arquivo. Se o Windows não
        /// permitir consultar o metadado, falha fechado para não remover um
        /// arquivo cuja identidade física não pôde ser confirmada.
        /// </summary>
        public static bool HasMultipleHardLinks(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

            uint linkCount;
            if (!TryGetHardLinkCount(path, out linkCount)) return true;
            return linkCount > 1;
        }

        public static bool IsWithinRoot(string candidate, string root)
        {
            string normalizedRoot = Normalize(root);
            string normalizedCandidate = Normalize(candidate);
            if (string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase)) return true;
            string rootedPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(rootedPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Caminho vazio.", "path");
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) && fullPath.Length > root.Length)
                return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath;
        }

        public static bool ContainsReparsePoint(string path)
        {
            string fullPath = Normalize(path);
            string root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return true;

            string relative = fullPath.Substring(root.Length);
            string current = root;
            foreach (string segment in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (!PathExists(current)) return true;
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        return true;
                }
                catch
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PathExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private static bool TryGetHardLinkCount(string path, out uint linkCount)
        {
            linkCount = 0;
            IntPtr handle = CreateFile(path, 0, FileShareRead | FileShareWrite | FileShareDelete,
                IntPtr.Zero, OpenExisting, FileFlagBackupSemantics, IntPtr.Zero);
            if (handle == InvalidHandleValue) return false;

            try
            {
                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(handle, out information)) return false;
                linkCount = information.NumberOfLinks;
                return true;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint FileShareDelete = 0x00000004;
        private const uint OpenExisting = 3;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(IntPtr fileHandle, out ByHandleFileInformation information);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        private static bool IsProtected(string path)
        {
            string normalized = Normalize(path);

            // Perfis de navegador inteiros sao protegidos, mas as subpastas de
            // cache conhecidas entram no catalogo REVIEW com um alvo menor e
            // guardas de processo. Isso preserva logins, cookies, historico,
            // sessoes e Downloads por construcao.
            if (IsKnownBrowserCachePath(normalized)) return false;

            string comparison = normalized + Path.DirectorySeparatorChar;

            foreach (string fragment in ProtectedPathFragments)
            {
                if (comparison.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            string[] segments = normalized.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string segment in segments)
            {
                foreach (string protectedName in ProtectedDirectoryNames)
                {
                    if (string.Equals(segment, protectedName, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }

        private static bool IsKnownBrowserCachePath(string normalized)
        {
            foreach (string root in BrowserCacheRoots)
            {
                int rootIndex = normalized.IndexOf(root, StringComparison.OrdinalIgnoreCase);
                if (rootIndex < 0) continue;

                string suffix = normalized.Substring(rootIndex + root.Length);
                string[] segments = suffix.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                for (int index = 0; index < segments.Length; index++)
                {
                    if (BrowserCacheDirectoryNames.Any(name => string.Equals(name, segments[index], StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
            }

            return false;
        }
    }
}
