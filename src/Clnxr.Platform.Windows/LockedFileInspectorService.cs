using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Clnxr.Core;
using Clnxr.Safety;

namespace Clnxr.Platform.Windows
{
    public sealed class LockedProcessInfo
    {
        public LockedProcessInfo(int processId, string applicationName, string serviceName, string applicationType)
        {
            ProcessId = processId;
            ApplicationName = applicationName ?? string.Empty;
            ServiceName = serviceName ?? string.Empty;
            ApplicationType = applicationType ?? string.Empty;
        }

        public int ProcessId { get; private set; }
        public string ApplicationName { get; private set; }
        public string ServiceName { get; private set; }
        public string ApplicationType { get; private set; }
    }

    public sealed class LockedFileInspection
    {
        public LockedFileInspection(string path)
        {
            Path = path ?? string.Empty;
            Processes = new List<LockedProcessInfo>();
            Issues = new List<string>();
        }

        public string Path { get; private set; }
        public bool Supported { get; internal set; }
        public IList<LockedProcessInfo> Processes { get; private set; }
        public IList<string> Issues { get; private set; }
    }

    /// <summary>
    /// Uses the Windows Restart Manager inventory API only. It never shuts down
    /// or restarts an application and never changes the inspected file.
    /// </summary>
    public sealed class LockedFileInspectorService
    {
        private const int ErrorMoreData = 234;
        private const int RmRebootReasonNone = 0;

        public LockedFileInspection Inspect(string path)
        {
            string normalized;
            try { normalized = PathSafetyPolicy.Normalize(path); }
            catch (Exception ex)
            {
                LockedFileInspection invalid = new LockedFileInspection(path);
                invalid.Issues.Add(PathRedactor.Redact("Caminho inválido: " + ex.Message));
                return invalid;
            }

            LockedFileInspection result = new LockedFileInspection(normalized);
            if (!File.Exists(normalized))
            {
                result.Issues.Add(PathRedactor.Redact("O arquivo não existe ou não está disponível."));
                return result;
            }

            uint sessionHandle;
            StringBuilder sessionKey = new StringBuilder(64);
            int status = RmStartSession(out sessionHandle, 0, sessionKey);
            if (status != 0)
            {
                result.Issues.Add("Restart Manager não pôde iniciar a sessão: " + FormatError(status));
                return result;
            }

            result.Supported = true;
            try
            {
                string[] resources = new[] { normalized };
                status = RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, IntPtr.Zero, 0, null);
                if (status != 0)
                {
                    result.Issues.Add("Não foi possível registrar o arquivo no Restart Manager: " + FormatError(status));
                    return result;
                }

                uint needed = 0;
                uint count = 0;
                uint rebootReasons = RmRebootReasonNone;
                status = RmGetList(sessionHandle, out needed, ref count, null, ref rebootReasons);
                if (status == ErrorMoreData && needed > 0)
                {
                    RM_PROCESS_INFO[] processes = new RM_PROCESS_INFO[needed];
                    count = needed;
                    status = RmGetList(sessionHandle, out needed, ref count, processes, ref rebootReasons);
                    if (status == 0)
                    {
                        for (int index = 0; index < count; index++)
                        {
                            RM_PROCESS_INFO process = processes[index];
                            result.Processes.Add(new LockedProcessInfo(process.Process.dwProcessId,
                                PathRedactor.Redact(process.strAppName),
                                PathRedactor.Redact(process.strServiceShortName),
                                ApplicationTypeText(process.ApplicationType)));
                        }
                    }
                }
                else if (status != 0)
                {
                    result.Issues.Add("Não foi possível consultar processos: " + FormatError(status));
                }
            }
            finally
            {
                RmEndSession(sessionHandle);
            }
            return result;
        }

        private static string ApplicationTypeText(RM_APP_TYPE type)
        {
            return type.ToString();
        }

        private static string FormatError(int status)
        {
            try { return new Win32Exception(status).Message; }
            catch { return status.ToString(); }
        }

        private enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string strServiceShortName;
            public RM_APP_TYPE ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint dwSessionHandle, uint nFiles, string[] rgsFileNames,
            uint nApplications, IntPtr rgApplications, uint nServices, string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint dwSessionHandle);
    }
}
