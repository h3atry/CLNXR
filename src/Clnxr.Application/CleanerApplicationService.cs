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
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Receipt = receipt;
            ReceiptPath = receiptPath ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public MaintenanceReceipt Receipt { get; private set; }
        public string ReceiptPath { get; private set; }
    }

    public sealed class CleanerApplicationService
    {
        private readonly WindowsCandidateScanner scanner;
        private readonly CleanupExecutor cleanupExecutor;
        private readonly ReceiptStore receiptStore;
        private readonly RecycleBinService recycleBinService;
        private readonly StorageSenseLauncher storageSenseLauncher;
        private readonly StorageAnalysisService storageAnalysisService;

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
        {
            if (safetyPolicy == null) throw new ArgumentNullException("safetyPolicy");
            if (processInspector == null) throw new ArgumentNullException("processInspector");
            if (receiptStore == null) throw new ArgumentNullException("receiptStore");
            if (recycleBinService == null) throw new ArgumentNullException("recycleBinService");
            if (storageSenseLauncher == null) throw new ArgumentNullException("storageSenseLauncher");
            if (storageAnalysisService == null) throw new ArgumentNullException("storageAnalysisService");

            scanner = new WindowsCandidateScanner(safetyPolicy);
            cleanupExecutor = new CleanupExecutor(safetyPolicy, processInspector);
            this.receiptStore = receiptStore;
            this.recycleBinService = recycleBinService;
            this.storageSenseLauncher = storageSenseLauncher;
            this.storageAnalysisService = storageAnalysisService;
        }

        public ScanSession Analyze(ScanProfile profile, CancellationToken cancellationToken, Action<string> progress)
        {
            return scanner.Scan(new ScanOptions(profile), cancellationToken, progress);
        }

        public ScanSession Analyze(ScanProfile profile, IEnumerable<string> selectedRuleIds, CancellationToken cancellationToken, Action<string> progress)
        {
            return scanner.Scan(new ScanOptions(profile, selectedRuleIds), cancellationToken, progress);
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
    }
}
