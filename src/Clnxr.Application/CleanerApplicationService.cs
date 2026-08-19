using System;
using System.Collections.Generic;
using System.Threading;
using Clnxr.Actions;
using Clnxr.Core;
using Clnxr.Evidence;
using Clnxr.Platform.Windows;
using Clnxr.Safety;

namespace Clnxr.Application
{
    public sealed class CleanupExecution
    {
        public CleanupExecution(CleanupReceipt receipt, string receiptPath)
        {
            if (receipt == null) throw new ArgumentNullException("receipt");
            Receipt = receipt;
            ReceiptPath = receiptPath ?? string.Empty;
        }

        public CleanupReceipt Receipt { get; private set; }
        public string ReceiptPath { get; private set; }
    }

    public sealed class ToolExecution
    {
        public ToolExecution(bool succeeded, string message, MaintenanceReceipt receipt, string receiptPath)
            : this(succeeded, message, string.Empty, receipt, receiptPath)
        {
        }

        public ToolExecution(bool succeeded, string message, string output, MaintenanceReceipt receipt, string receiptPath)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Output = output ?? string.Empty;
            Receipt = receipt;
            ReceiptPath = receiptPath ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public string Output { get; private set; }
        public MaintenanceReceipt Receipt { get; private set; }
        public string ReceiptPath { get; private set; }
    }

    public sealed class CleanerApplicationService
    {
        private readonly PathSafetyPolicy safetyPolicy;
        private readonly WindowsCandidateScanner scanner;
        private readonly CleanupExecutor cleanupExecutor;
        private readonly ReceiptStore receiptStore;
        private readonly RecycleBinService recycleBinService;
        private readonly StorageSenseLauncher storageSenseLauncher;
        private readonly StorageAnalysisService storageAnalysisService;
        private readonly CustomRuleStore customRuleStore;
        private readonly StartupExplorerService startupExplorerService;
        private readonly LockedFileInspectorService lockedFileInspectorService;
        private readonly UninstallResidualService uninstallResidualService;
        private readonly ScheduledCleanupService scheduledCleanupService;
        private readonly NetworkUtilitiesService networkUtilitiesService;
        private readonly SystemRepairService systemRepairService;
        private readonly UserDataCleanupService userDataCleanupService;

        public CleanerApplicationService()
            : this(new PathSafetyPolicy(), new WindowsProcessInspector(), ReceiptStore.CreateDefault(), new RecycleBinService(), new StorageSenseLauncher(), new StorageAnalysisService())
        {
        }

        public CleanerApplicationService(PathSafetyPolicy safetyPolicy, IProcessInspector processInspector, ReceiptStore receiptStore,
            RecycleBinService recycleBinService, StorageSenseLauncher storageSenseLauncher)
            : this(safetyPolicy, processInspector, receiptStore, recycleBinService, storageSenseLauncher, new StorageAnalysisService())
        {
        }

        public CleanerApplicationService(PathSafetyPolicy safetyPolicy, IProcessInspector processInspector, ReceiptStore receiptStore,
            RecycleBinService recycleBinService, StorageSenseLauncher storageSenseLauncher, StorageAnalysisService storageAnalysisService)
            : this(safetyPolicy, processInspector, receiptStore, recycleBinService, storageSenseLauncher, storageAnalysisService, CustomRuleStore.CreateDefault())
        {
        }

        public CleanerApplicationService(PathSafetyPolicy safetyPolicy, IProcessInspector processInspector, ReceiptStore receiptStore,
            RecycleBinService recycleBinService, StorageSenseLauncher storageSenseLauncher, StorageAnalysisService storageAnalysisService,
            CustomRuleStore customRuleStore)
        {
            if (safetyPolicy == null) throw new ArgumentNullException("safetyPolicy");
            if (processInspector == null) throw new ArgumentNullException("processInspector");
            if (receiptStore == null) throw new ArgumentNullException("receiptStore");
            if (recycleBinService == null) throw new ArgumentNullException("recycleBinService");
            if (storageSenseLauncher == null) throw new ArgumentNullException("storageSenseLauncher");
            if (storageAnalysisService == null) throw new ArgumentNullException("storageAnalysisService");
            if (customRuleStore == null) throw new ArgumentNullException("customRuleStore");

            this.safetyPolicy = safetyPolicy;
            scanner = new WindowsCandidateScanner(safetyPolicy);
            cleanupExecutor = new CleanupExecutor(safetyPolicy, processInspector);
            this.receiptStore = receiptStore;
            this.recycleBinService = recycleBinService;
            this.storageSenseLauncher = storageSenseLauncher;
            this.storageAnalysisService = storageAnalysisService;
            this.customRuleStore = customRuleStore;
            startupExplorerService = new StartupExplorerService();
            lockedFileInspectorService = new LockedFileInspectorService();
            uninstallResidualService = new UninstallResidualService();
            scheduledCleanupService = new ScheduledCleanupService();
            networkUtilitiesService = new NetworkUtilitiesService();
            systemRepairService = new SystemRepairService();
            userDataCleanupService = new UserDataCleanupService();
        }

        public ScanSession Analyze(ScanProfile profile, CancellationToken cancellationToken, Action<string> progress)
        {
            return scanner.Scan(new ScanOptions(profile), cancellationToken, progress);
        }

        public ScanSession Analyze(ScanProfile profile, IEnumerable<string> selectedRuleIds, CancellationToken cancellationToken, Action<string> progress)
        {
            return scanner.Scan(new ScanOptions(profile, selectedRuleIds), cancellationToken, progress);
        }

        public ScanSession Analyze(ScanProfile profile, IEnumerable<string> selectedRuleIds, IEnumerable<CustomRuleDefinition> customRules,
            CancellationToken cancellationToken, Action<string> progress)
        {
            return scanner.Scan(new ScanOptions(profile, selectedRuleIds, customRules), cancellationToken, progress);
        }

        public CustomRulePreview PreviewCustomRule(CustomRuleDraft draft, CancellationToken cancellationToken, Action<string> progress)
        {
            return new CustomRuleService(safetyPolicy).Preview(draft, cancellationToken, progress);
        }

        public CustomRuleDefinition SaveCustomRule(CustomRuleDraft draft, CancellationToken cancellationToken, Action<string> progress)
        {
            CustomRulePreview preview = PreviewCustomRule(draft, cancellationToken, progress);
            if (!preview.CanSave || preview.Definition == null)
                throw new InvalidOperationException("A regra personalizada só pode ser salva após uma prévia concluída sem avisos.");
            customRuleStore.Save(preview.Definition);
            return preview.Definition;
        }

        public IList<CustomRuleDefinition> ListCustomRules()
        {
            return customRuleStore.List();
        }

        public bool DeleteCustomRule(string ruleId)
        {
            return customRuleStore.Delete(ruleId);
        }

        public CleanupExecution Clean(ScanSession session, IEnumerable<string> selectedFindingIds, CancellationToken cancellationToken,
            Action<CleanupProgress> progress)
        {
            ActionPlan plan = ActionPlan.Create(session, selectedFindingIds);
            CleanupReceipt receipt = cleanupExecutor.Execute(plan, cancellationToken, progress);
            string receiptPath = receiptStore.Save(receipt);
            return new CleanupExecution(receipt, receiptPath);
        }

        public RecycleBinSnapshot QueryRecycleBin()
        {
            return recycleBinService.QueryAllVolumes();
        }

        public ToolExecution EmptyRecycleBin(RecycleBinSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.Available)
                throw new InvalidOperationException("A Lixeira precisa ser consultada antes do esvaziamento.");

            MaintenanceReceipt receipt = new MaintenanceReceipt("recycle-bin-v1", snapshot.ItemCount, snapshot.Bytes);
            RecycleBinEmptyResult result = recycleBinService.EmptyAllVolumes();
            receipt.CompletedUtc = DateTime.UtcNow;
            receipt.Status = result.Succeeded ? ToolActionStatus.Succeeded : ToolActionStatus.Failed;
            receipt.Message = result.Message;
            string receiptPath = receiptStore.SaveMaintenance(receipt);
            return new ToolExecution(result.Succeeded, result.Message, receipt, receiptPath);
        }

        public ToolExecution OpenStorageSense()
        {
            MaintenanceReceipt receipt = new MaintenanceReceipt("storage-sense-launch-v1", 0, 0);
            StorageSenseLaunchResult result = storageSenseLauncher.OpenSettings();
            receipt.CompletedUtc = DateTime.UtcNow;
            receipt.Status = result.Succeeded ? ToolActionStatus.Succeeded : ToolActionStatus.Failed;
            receipt.Message = result.Message;
            string receiptPath = receiptStore.SaveMaintenance(receipt);
            return new ToolExecution(result.Succeeded, result.Message, receipt, receiptPath);
        }

        public IList<string> ListReceiptPaths()
        {
            return receiptStore.ListReceiptPaths();
        }

        public ReceiptFileVerification VerifyReceiptFile(string path)
        {
            return receiptStore.VerifyFile(path);
        }

        public ReceiptDocument ReadReceiptDocument(string path)
        {
            return receiptStore.ReadDocument(path);
        }

        public IList<Rule> ListRules()
        {
            return WindowsRuleCatalog.GetAllRules();
        }

        public StorageAnalysisResult AnalyzeDiskMap(CancellationToken cancellationToken, Action<StorageAnalysisProgress> progress)
        {
            return storageAnalysisService.BuildDiskMap(cancellationToken, progress);
        }

        public StorageAnalysisResult FindLargeFiles(long minimumBytes, int maximumResults, CancellationToken cancellationToken,
            Action<StorageAnalysisProgress> progress)
        {
            return storageAnalysisService.FindLargeFiles(minimumBytes, maximumResults, cancellationToken, progress);
        }

        public StorageAnalysisResult FindDuplicates(long minimumBytes, int maximumFilesToHash, CancellationToken cancellationToken,
            Action<StorageAnalysisProgress> progress)
        {
            return storageAnalysisService.FindDuplicates(minimumBytes, maximumFilesToHash, cancellationToken, progress);
        }

        public StartupExplorerResult ListStartupEntries()
        {
            return startupExplorerService.ListEntries();
        }

        public LockedFileInspection InspectLockedFile(string path)
        {
            return lockedFileInspectorService.Inspect(path);
        }

        public StartupMutationResult DisableStartupEntry(StartupEntry entry)
        {
            return startupExplorerService.Disable(entry);
        }

        public IList<DisabledStartupEntry> ListDisabledStartupEntries()
        {
            return startupExplorerService.ListDisabledEntries();
        }

        public StartupMutationResult RestoreStartupEntry(DisabledStartupEntry entry)
        {
            return startupExplorerService.Restore(entry);
        }

        public UninstallResidualResult ListUninstallResiduals()
        {
            return uninstallResidualService.ListEntries();
        }

        public ToolExecution ScheduleSafeDailyCleanup(string executablePath)
        {
            ScheduledCleanupPlan plan = scheduledCleanupService.BuildSafeDailyPlan(executablePath);
            ScheduledCleanupResult result = scheduledCleanupService.Create(plan);
            MaintenanceReceipt receipt = new MaintenanceReceipt("scheduled-safe-cleanup-v1", 0, 0);
            receipt.CompletedUtc = DateTime.UtcNow;
            receipt.Status = result.Succeeded ? ToolActionStatus.Succeeded : ToolActionStatus.Failed;
            receipt.Message = result.Message;
            string receiptPath = receiptStore.SaveMaintenance(receipt);
            return new ToolExecution(result.Succeeded, result.Message, receipt, receiptPath);
        }

        public ToolExecution RemoveScheduledSafeCleanup()
        {
            ScheduledCleanupResult result = scheduledCleanupService.Remove();
            MaintenanceReceipt receipt = new MaintenanceReceipt("scheduled-safe-cleanup-remove-v1", 0, 0);
            receipt.CompletedUtc = DateTime.UtcNow;
            receipt.Status = result.Succeeded ? ToolActionStatus.Succeeded : ToolActionStatus.Failed;
            receipt.Message = result.Message;
            string receiptPath = receiptStore.SaveMaintenance(receipt);
            return new ToolExecution(result.Succeeded, result.Message, receipt, receiptPath);
        }

        public IList<NetworkActionPlan> ListNetworkPlans()
        {
            return networkUtilitiesService.ListPlans();
        }

        public NetworkDiagnosticResult DiagnoseNetwork()
        {
            return networkUtilitiesService.Diagnose();
        }

        public ToolExecution ExecuteNetworkAction(string actionId)
        {
            NetworkActionPlan plan = networkUtilitiesService.BuildPlan(actionId);
            NetworkActionResult result = networkUtilitiesService.Execute(plan);
            MaintenanceReceipt receipt = new MaintenanceReceipt("network-" + plan.ActionId + "-v1", 0, 0);
            receipt.CompletedUtc = DateTime.UtcNow;
            receipt.Status = result.Succeeded ? ToolActionStatus.Succeeded : ToolActionStatus.Failed;
            receipt.Message = FormatToolMessage(result.Message, result.Command, result.Output, result.Issues);
            string receiptPath = receiptStore.SaveMaintenance(receipt);
            return new ToolExecution(result.Succeeded, result.Message, result.Output, receipt, receiptPath);
        }

        public IList<SystemRepairPlan> ListSystemRepairPlans()
        {
            return systemRepairService.ListPlans();
        }

        public UserDataCleanupPreview PreviewUserDataCleanup(CancellationToken cancellationToken)
        {
            return userDataCleanupService.Preview(cancellationToken);
        }

        public UserDataCleanupResult CleanupUserData(CancellationToken cancellationToken)
        {
            return userDataCleanupService.Execute(cancellationToken);
        }

        public ToolExecution ExecuteSystemRepair(string actionId, string volume)
        {
            SystemRepairPlan plan = systemRepairService.BuildPlan(actionId, volume);
            SystemRepairResult result = systemRepairService.Execute(plan);
            MaintenanceReceipt receipt = new MaintenanceReceipt("system-repair-" + plan.ActionId + "-v1", 0, 0);
            receipt.CompletedUtc = DateTime.UtcNow;
            receipt.Status = result.Succeeded ? ToolActionStatus.Succeeded : ToolActionStatus.Failed;
            receipt.Message = FormatToolMessage(result.Message, result.Command, result.Output, result.Issues);
            string receiptPath = receiptStore.SaveMaintenance(receipt);
            return new ToolExecution(result.Succeeded, result.Message, result.Output, receipt, receiptPath);
        }

        private static string FormatToolMessage(string message, string command, string output, IList<string> issues)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(message)) parts.Add(message);
            if (!string.IsNullOrWhiteSpace(command)) parts.Add("Comando fixo: " + PathRedactor.Redact(command));
            if (issues != null)
            {
                foreach (string issue in issues)
                    if (!string.IsNullOrWhiteSpace(issue)) parts.Add("Aviso: " + PathRedactor.Redact(issue));
            }
            if (!string.IsNullOrWhiteSpace(output)) parts.Add("Saída:\r\n" + PathRedactor.Redact(output));
            string result = string.Join("\r\n", parts.ToArray());
            const int max = 16000;
            return result.Length <= max ? result : result.Substring(0, max) + "\r\n[recibo truncado pelo limite local]";
        }
    }
}
