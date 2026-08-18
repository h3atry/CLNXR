using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Clnxr.Actions;
using Clnxr.Application;
using Clnxr.Core;
using Clnxr.Evidence;
using Clnxr.Platform.Windows;
using Clnxr.Safety;

namespace Clnxr.Safety.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            string fixtureRoot = Path.Combine(Path.GetTempPath(), "clnxr-safety-fixture-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(fixtureRoot);
                TestPolicyGuards(fixtureRoot);
                TestCleanupAndReceipt(fixtureRoot);
                TestCancellationAndProcessGuard(fixtureRoot);
                TestLockedFileEvidence(fixtureRoot);
                TestProfileCatalog();
                TestDeclarativeRulePack();
                TestBrowserCacheRulesAndAge(fixtureRoot);
                TestReadOnlyStorageTools(fixtureRoot);
                TestStorageAnalysisMidTreeCancellation(fixtureRoot);
                TestJunctionGuard(fixtureRoot);
                TestDirectorySymlinkGuard(fixtureRoot);
                Console.WriteLine("PASS: 11 grupos de testes de seguranca, evidencia, catalogo declarativo e ferramentas somente leitura foram concluidos.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
            finally
            {
                DeleteFixtureRoot(fixtureRoot);
            }
        }

        private static void TestPolicyGuards(string fixtureRoot)
        {
            string cache = Path.Combine(fixtureRoot, "Cache");
            string downloads = Path.Combine(fixtureRoot, "Downloads", "Cache");
            string outside = Path.Combine(fixtureRoot, "Outside");
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(downloads);
            Directory.CreateDirectory(outside);

            Rule safeRule = CreateRule("test-safe", RiskLevel.Safe);
            PathSafetyPolicy policy = new PathSafetyPolicy();
            Expect(policy.ValidateFinding(new Finding(null, safeRule, "T:\\", cache, cache, null, 0, 0)).Allowed,
                "Um cache dentro da raiz de teste precisa ser permitido.");
            Expect(!policy.ValidateFinding(new Finding(null, safeRule, "T:\\", downloads, downloads, null, 0, 0)).Allowed,
                "Um caminho sob Downloads precisa ser bloqueado.");
            Expect(!policy.ValidateFinding(new Finding(null, safeRule, "T:\\", cache, outside, null, 0, 0)).Allowed,
                "Um alvo fora da raiz aprovada precisa ser bloqueado.");

            Rule blockedRule = CreateRule("test-blocked", RiskLevel.Blocked);
            Expect(!policy.ValidateFinding(new Finding(null, blockedRule, "T:\\", cache, cache, null, 0, 0)).Allowed,
                "Uma regra BLOCKED precisa ser bloqueada independentemente do caminho.");
        }

        private static void TestCleanupAndReceipt(string fixtureRoot)
        {
            string root = Path.Combine(fixtureRoot, "cleanup");
            Directory.CreateDirectory(root);
            string directFile = Path.Combine(root, "shader.bin");
            string nestedDirectory = Path.Combine(root, "nested");
            Directory.CreateDirectory(nestedDirectory);
            string nestedFile = Path.Combine(nestedDirectory, "cache.bin");
            File.WriteAllBytes(directFile, new byte[] { 1, 2, 3, 4 });
            File.WriteAllBytes(nestedFile, new byte[] { 5, 6, 7, 8, 9, 10 });

            Rule rule = CreateRule("fixture-cleanup-v1", RiskLevel.Safe);
            Finding finding = new Finding("fixture-cleanup", rule, "T:\\", root, root, null, 10, 2);
            ScanSession session = CreateReviewedSession(finding);
            ActionPlan plan = ActionPlan.Create(session, new[] { finding.FindingId });
            ReceiptStore store = new ReceiptStore(Path.Combine(fixtureRoot, "receipts"));
            CleanerApplicationService application = new CleanerApplicationService(new PathSafetyPolicy(), new FakeProcessInspector(false), store,
                new RecycleBinService(), new StorageSenseLauncher());
            CleanupExecution execution = application.Clean(session, new[] { finding.FindingId }, CancellationToken.None, null);
            CleanupReceipt receipt = execution.Receipt;

            Expect(receipt.TotalFilesRemoved == 2, "A limpeza de fixture precisa remover dois arquivos.");
            Expect(receipt.TotalBytesRemoved == 10, "A limpeza de fixture precisa registrar bytes reais removidos.");
            Expect(receipt.SchemaVersion == ReceiptSchema.CurrentVersion, "Recibo de limpeza precisa registrar a versao de esquema atual.");
            Expect(!File.Exists(directFile) && !File.Exists(nestedFile), "Os arquivos da fixture precisam ser removidos.");

            string receiptPath = execution.ReceiptPath;
            Expect(File.Exists(receiptPath), "O recibo precisa ser persistido localmente.");
            Expect(store.Verify(receipt), "O hash do recibo precisa validar apos a gravacao.");
            Expect(store.VerifyFile(receiptPath).IsValid, "O hash do recibo salvo precisa validar a partir do arquivo local.");
            ReceiptDocument cleanupDocument = store.ReadDocument(receiptPath);
            Expect(cleanupDocument.SchemaVersion == ReceiptSchema.CurrentVersion, "O visualizador estruturado precisa informar o esquema do recibo de limpeza.");
            Expect(cleanupDocument.Details.Any(detail => detail.Section == "Resultado 1" && detail.Field == "TargetPath"),
                "O visualizador estruturado precisa listar os campos do resultado de limpeza.");
            ReceiptDetail redactedTarget = cleanupDocument.Details.First(detail => detail.Section == "Resultado 1" && detail.Field == "TargetPath");
            Expect(redactedTarget.Value.IndexOf(Environment.UserName, StringComparison.OrdinalIgnoreCase) < 0,
                "O recibo nao pode persistir o nome do usuario no caminho completo do alvo.");

            string tamperedPath = Path.Combine(fixtureRoot, "receipts", "receipt-tampered.json");
            string tamperedPayload = File.ReadAllText(receiptPath).Replace("fixture-cleanup-v1", "fixture-cleanup-v9");
            File.WriteAllText(tamperedPath, tamperedPayload);
            Expect(!store.VerifyFile(tamperedPath).IsValid, "A verificacao de arquivo precisa detectar alteracao no conteudo do recibo.");

            MaintenanceReceipt maintenanceReceipt = new MaintenanceReceipt("fixture-tool", 2, 10);
            maintenanceReceipt.CompletedUtc = DateTime.UtcNow;
            maintenanceReceipt.Status = ToolActionStatus.Succeeded;
            maintenanceReceipt.Message = "Ferramenta de fixture concluida.";
            Expect(maintenanceReceipt.SchemaVersion == ReceiptSchema.CurrentVersion, "Recibo de ferramenta precisa registrar a versao de esquema atual.");
            string maintenancePath = store.SaveMaintenance(maintenanceReceipt);
            Expect(File.Exists(maintenancePath), "O recibo de ferramenta precisa ser persistido localmente.");
            Expect(store.VerifyMaintenance(maintenanceReceipt), "O hash do recibo de ferramenta precisa validar apos a gravacao.");
            Expect(store.VerifyFile(maintenancePath).IsValid, "O hash do recibo de ferramenta salvo precisa validar a partir do arquivo local.");
            ReceiptDocument maintenanceDocument = store.ReadDocument(maintenancePath);
            Expect(maintenanceDocument.Details.Any(detail => detail.Field == "ToolId" && detail.Value == "fixture-tool"),
                "O visualizador estruturado precisa listar os campos do recibo de ferramenta.");
        }

        private static void TestCancellationAndProcessGuard(string fixtureRoot)
        {
            string cancellationRoot = Path.Combine(fixtureRoot, "cancelled");
            Directory.CreateDirectory(cancellationRoot);
            string cancellationFile = Path.Combine(cancellationRoot, "keep.bin");
            File.WriteAllBytes(cancellationFile, new byte[] { 1 });

            Finding cancellationFinding = new Finding("fixture-cancel", CreateRule("fixture-cancel-v1", RiskLevel.Safe), "T:\\",
                cancellationRoot, cancellationRoot, null, 1, 1);
            CleanupExecutor executor = new CleanupExecutor(new PathSafetyPolicy(), new FakeProcessInspector(false));
            CleanupReceipt cancelled = executor.Execute(ActionPlan.Create(CreateReviewedSession(cancellationFinding), new[] { cancellationFinding.FindingId }),
                new CancellationToken(true), null);
            Expect(cancelled.WasCancelled, "Token cancelado precisa produzir recibo cancelado.");
            Expect(File.Exists(cancellationFile), "Arquivo precisa permanecer quando a limpeza foi cancelada antes de iniciar.");

            string processRoot = Path.Combine(fixtureRoot, "process-guard");
            Directory.CreateDirectory(processRoot);
            string processFile = Path.Combine(processRoot, "keep-too.bin");
            File.WriteAllBytes(processFile, new byte[] { 1 });
            Finding processFinding = new Finding("fixture-process", CreateRule("fixture-process-v1", RiskLevel.Safe, new[] { "fakeprocess" }), "T:\\",
                processRoot, processRoot, null, 1, 1);
            CleanupExecutor guardedExecutor = new CleanupExecutor(new PathSafetyPolicy(), new FakeProcessInspector(true));
            CleanupReceipt guarded = guardedExecutor.Execute(ActionPlan.Create(CreateReviewedSession(processFinding), new[] { processFinding.FindingId }),
                CancellationToken.None, null);
            Expect(guarded.Results.Single().Status == ActionStatus.Skipped, "Processo em uso precisa pular o achado.");
            Expect(File.Exists(processFile), "Arquivo precisa permanecer quando o processo relacionado esta em uso.");
            Expect(guarded.TotalItemsSkipped == 1, "Processo relacionado em uso precisa registrar o item preservado no recibo.");
        }

        private static void TestLockedFileEvidence(string fixtureRoot)
        {
            string lockedRoot = Path.Combine(fixtureRoot, "locked-file");
            Directory.CreateDirectory(lockedRoot);
            string lockedFile = Path.Combine(lockedRoot, "locked.bin");
            File.WriteAllBytes(lockedFile, new byte[] { 7, 8, 9 });

            Finding finding = new Finding("fixture-locked", CreateRule("fixture-locked-v1", RiskLevel.Safe), "T:\\",
                lockedRoot, lockedRoot, null, 3, 1);
            CleanupReceipt receipt;
            using (FileStream lockHandle = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                receipt = new CleanupExecutor(new PathSafetyPolicy(), new FakeProcessInspector(false)).Execute(
                    ActionPlan.Create(CreateReviewedSession(finding), new[] { finding.FindingId }), CancellationToken.None, null);
            }

            Expect(File.Exists(lockedFile), "Arquivo com lock real precisa ser preservado, sem forcar exclusao.");
            Expect(receipt.Results.Single().Status == ActionStatus.Skipped, "Alvo com arquivo bloqueado precisa ficar como pulado quando nada foi removido.");
            Expect(receipt.TotalItemsSkipped >= 1, "Recibo precisa contabilizar o arquivo bloqueado como item preservado/pulado.");
        }

        private static void TestProfileCatalog()
        {
            IList<Rule> safe = WindowsRuleCatalog.GetRules(ScanProfile.Safe);
            IList<Rule> complete = WindowsRuleCatalog.GetRules(ScanProfile.Complete);
            IList<Rule> gaming = WindowsRuleCatalog.GetRules(ScanProfile.Gaming);
            IList<Rule> developer = WindowsRuleCatalog.GetRules(ScanProfile.Developer);

            Expect(safe.Any(rule => rule.RuleId == "profile-temp-v1"), "Perfil Seguro precisa incluir temporarios de perfil.");
            Expect(!safe.Any(rule => rule.RuleId == "windows-temp-v1"), "Perfil Seguro nao pode incluir temporarios do Windows em REVIEW.");
            Expect(complete.Any(rule => rule.RuleId == "directx-cache-v1"), "Perfil Completo precisa incluir cache DirectX em REVIEW.");
            Expect(complete.Any(rule => rule.RuleId == "intel-shader-cache-v1"), "Perfil Completo precisa incluir cache Intel em REVIEW.");
            Expect(complete.Any(rule => rule.RuleId == "chrome-cache-v1"), "Perfil Completo precisa incluir cache do Chrome em REVIEW.");
            Expect(complete.Any(rule => rule.RuleId == "firefox-cache-v1"), "Perfil Completo precisa incluir cache do Firefox em REVIEW.");
            Expect(gaming.Any(rule => rule.RuleId == "unreal-derived-data-cache-v1"), "Perfil Jogos precisa incluir a regra Unreal declarada.");
            Expect(!gaming.Any(rule => rule.RuleId == "nuget-http-cache-v1"), "Perfil Jogos nao pode incluir cache de desenvolvedor.");
            Expect(developer.Any(rule => rule.RuleId == "nuget-http-cache-v1"), "Perfil Desenvolvedor precisa incluir cache NuGet em REVIEW.");
            Expect(developer.Any(rule => rule.RuleId == "pnpm-store-v1"), "Perfil Desenvolvedor precisa incluir store pnpm em REVIEW.");
            Expect(developer.Any(rule => rule.RuleId == "yarn-cache-v1"), "Perfil Desenvolvedor precisa incluir cache Yarn em REVIEW.");
            Expect(developer.Any(rule => rule.RuleId == "uv-cache-v1"), "Perfil Desenvolvedor precisa incluir cache uv em REVIEW.");
            Expect(developer.Any(rule => rule.RuleId == "gradle-cache-v1" && rule.Risk == RiskLevel.Advanced), "Cache Gradle precisa ser ADVANCED e explicitamente revisado.");
            Expect(complete.Any(rule => rule.RuleId == "discord-cache-v1"), "Perfil Completo precisa incluir cache Discord isolado.");
            Expect(complete.Any(rule => rule.RuleId == "teams-cache-v1"), "Perfil Completo precisa incluir cache Teams isolado.");
            Expect(complete.Any(rule => rule.RuleId == "spotify-cache-roaming-v1"), "Perfil Completo precisa incluir cache Spotify em Roaming isolado.");
            Expect(complete.Any(rule => rule.RuleId == "spotify-cache-local-v1"), "Perfil Completo precisa incluir cache Spotify em Local isolado.");
            Expect(complete.Any(rule => rule.RuleId == "electron-cache-v1"), "Perfil Completo precisa incluir somente o cache Electron delimitado.");
            Expect(safe.Single(rule => rule.RuleId == "wer-report-archive-v1").MinimumAgeDays == 7, "Relatorios WER precisam de idade minima de sete dias.");
            Expect(!safe.Any(rule => rule.RuleId == "chrome-cache-v1"), "Cache de navegador nao pode entrar no perfil Seguro.");
            IList<Rule> personalized = WindowsRuleCatalog.GetRules(ScanProfile.Personalized, new[] { "chrome-cache-v1" });
            Expect(personalized.Count == 1 && personalized[0].RuleId == "chrome-cache-v1", "Perfil Personalizado precisa aceitar somente IDs de regras explicitamente escolhidos.");

            Uri storageSenseUri = new Uri(StorageSenseLauncher.SettingsUri);
            Expect(string.Equals(storageSenseUri.Scheme, "ms-settings", StringComparison.OrdinalIgnoreCase), "O launcher de limpeza oficial precisa usar URI de configuracao do Windows.");
        }

        private static void TestDeclarativeRulePack()
        {
            IList<Rule> rules = WindowsRuleCatalog.GetAllRules();
            Expect(WindowsRuleCatalog.CatalogVersion == "0.2.0", "Catalogo declarativo precisa expor a versao esperada.");
            Expect(rules.Count >= 30, "Pacote declarativo precisa carregar todas as regras Windows catalogadas.");
            Expect(rules.Select(rule => rule.RuleId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rules.Count,
                "Pacote declarativo nao pode carregar IDs de regras duplicados.");
            Expect(rules.All(rule => !string.IsNullOrWhiteSpace(rule.Version) && rule.Profiles.Count > 0),
                "Cada regra declarativa precisa ter versao e pelo menos um perfil.");
            Expect(rules.Any(rule => rule.RuleId == "windows-temp-v1" && rule.Risk == RiskLevel.Review),
                "Pacote declarativo precisa preservar o risco de temporarios do Windows.");
        }

        private static void TestBrowserCacheRulesAndAge(string fixtureRoot)
        {
            string browserCache = Path.Combine(fixtureRoot, "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Cache");
            string browserLogin = Path.Combine(fixtureRoot, "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Login Data");
            string firefoxCache = Path.Combine(fixtureRoot, "AppData", "Local", "Mozilla", "Firefox", "Profiles", "default", "cache2");
            string firefoxLogin = Path.Combine(fixtureRoot, "AppData", "Local", "Mozilla", "Firefox", "Profiles", "default", "logins.json");
            Directory.CreateDirectory(browserCache);
            Directory.CreateDirectory(browserLogin);
            Directory.CreateDirectory(firefoxCache);
            Directory.CreateDirectory(firefoxLogin);

            Rule browserRule = new Rule("fixture-browser-cache-v1", "1", "Cache de navegador", "Somente cache", RiskLevel.Review,
                RuleActionKind.DirectoryContents, new[] { "Teste" }, new[] { "chrome" });
            PathSafetyPolicy policy = new PathSafetyPolicy();
            Expect(policy.ValidateFinding(new Finding(null, browserRule, "T:\\", browserCache, browserCache, null, 0, 0)).Allowed,
                "Subpasta de cache conhecida do Chrome precisa ser permitida em REVIEW.");
            Expect(!policy.ValidateFinding(new Finding(null, browserRule, "T:\\", browserLogin, browserLogin, null, 0, 0)).Allowed,
                "Login Data do navegador precisa continuar protegido.");
            Expect(policy.ValidateFinding(new Finding(null, browserRule, "T:\\", firefoxCache, firefoxCache, null, 0, 0)).Allowed,
                "cache2 do Firefox precisa ser permitido somente como subpasta de cache.");
            Expect(!policy.ValidateFinding(new Finding(null, browserRule, "T:\\", firefoxLogin, firefoxLogin, null, 0, 0)).Allowed,
                "logins.json do Firefox precisa continuar protegido.");

            string profileRoot = Path.Combine(fixtureRoot, "browser-profiles");
            string defaultCache = Path.Combine(profileRoot, "Default", "Cache");
            string secondCache = Path.Combine(profileRoot, "Profile 1", "GPUCache");
            Directory.CreateDirectory(defaultCache);
            Directory.CreateDirectory(secondCache);
            IList<string> expanded = WindowsCandidateScanner.ExpandDirectoryPattern(Path.Combine(profileRoot, "*", "*Cache"));
            Expect(expanded.Count == 2 && expanded.Contains(defaultCache) && expanded.Contains(secondCache),
                "Expansao de perfis de navegador precisa localizar somente as subpastas de cache declaradas.");

            string ageRoot = Path.Combine(fixtureRoot, "age-filter");
            Directory.CreateDirectory(ageRoot);
            string oldFile = Path.Combine(ageRoot, "old.dmp");
            string recentFile = Path.Combine(ageRoot, "recent.dmp");
            File.WriteAllBytes(oldFile, new byte[] { 1, 2, 3, 4 });
            File.WriteAllBytes(recentFile, new byte[] { 5, 6, 7, 8, 9 });
            File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(recentFile, DateTime.UtcNow.AddDays(-1));

            MeasurementResult measurement = FileMeasurement.MeasureDirectory(ageRoot, CancellationToken.None, 7);
            Expect(measurement.FileCount == 1 && measurement.Bytes == 4, "Filtro de idade precisa medir somente o dump antigo.");

            Rule ageRule = new Rule("fixture-age-v1", "1", "Dumps antigos", "Somente arquivos com sete dias", RiskLevel.Safe,
                RuleActionKind.DirectoryContents, new[] { "Teste" }, new string[0], 7);
            Finding finding = new Finding("fixture-age", ageRule, "T:\\", ageRoot, ageRoot, null, 4, 1);
            CleanupReceipt receipt = new CleanupExecutor(policy, new FakeProcessInspector(false)).Execute(
                ActionPlan.Create(CreateReviewedSession(finding), new[] { finding.FindingId }), CancellationToken.None, null);
            Expect(!File.Exists(oldFile) && File.Exists(recentFile), "Limpeza com idade minima precisa remover somente o arquivo antigo.");
            Expect(receipt.TotalBytesRemoved == 4 && receipt.TotalItemsSkipped >= 1, "Recibo precisa separar bytes removidos e arquivo recente preservado.");
        }

        private static void TestReadOnlyStorageTools(string fixtureRoot)
        {
            string analysisRoot = Path.Combine(fixtureRoot, "storage-tools");
            string externalRoot = Path.Combine(fixtureRoot, "storage-tools-external");
            Directory.CreateDirectory(analysisRoot);
            Directory.CreateDirectory(externalRoot);

            string sameA = Path.Combine(analysisRoot, "same-a.bin");
            string sameB = Path.Combine(analysisRoot, "same-b.bin");
            string large = Path.Combine(analysisRoot, "large.bin");
            string external = Path.Combine(externalRoot, "preserve.bin");
            File.WriteAllBytes(sameA, new byte[] { 4, 3, 2, 1, 4, 3, 2, 1 });
            File.WriteAllBytes(sameB, new byte[] { 4, 3, 2, 1, 4, 3, 2, 1 });
            File.WriteAllBytes(large, Enumerable.Repeat((byte)9, 256).ToArray());
            File.WriteAllText(external, "must remain outside read-only analysis root");
            CreateJunction(Path.Combine(analysisRoot, "external-link"), externalRoot);

            StorageAnalysisService service = new StorageAnalysisService();
            StorageAnalysisResult map = service.BuildDiskMap(new[] { analysisRoot }, CancellationToken.None, null);
            Expect(!map.WasCancelled && map.DiskEntries.Count >= 3, "Mapa somente leitura precisa medir arquivos da fixture.");
            Expect(map.Issues.Any(issue => issue.Message.IndexOf("junction", StringComparison.OrdinalIgnoreCase) >= 0), "Mapa precisa registrar junction ignorada.");

            StorageAnalysisResult largeFiles = service.FindLargeFiles(new[] { analysisRoot }, 128, 10, CancellationToken.None, null);
            Expect(largeFiles.LargeFiles.Any(file => string.Equals(file.Path, large, StringComparison.OrdinalIgnoreCase)), "Ferramenta de arquivos grandes precisa encontrar o arquivo da fixture.");

            StorageAnalysisResult duplicates = service.FindDuplicates(new[] { analysisRoot }, 1, 20, CancellationToken.None, null);
            Expect(duplicates.DuplicateGroups.Any(group => group.Paths.Contains(sameA) && group.Paths.Contains(sameB)), "Ferramenta de duplicados precisa agrupar arquivos com hash igual.");
            Expect(File.Exists(sameA) && File.Exists(sameB) && File.Exists(large) && File.Exists(external), "Ferramentas somente leitura nao podem apagar arquivos da fixture nem o alvo da junction.");

            StorageAnalysisResult cancelled = service.FindLargeFiles(new[] { analysisRoot }, 0, 10, new CancellationToken(true), null);
            Expect(cancelled.WasCancelled, "Ferramenta somente leitura precisa respeitar cancelamento antes de percorrer arquivos.");
        }

        private static void TestStorageAnalysisMidTreeCancellation(string fixtureRoot)
        {
            string root = Path.Combine(fixtureRoot, "storage-cancellation");
            Directory.CreateDirectory(root);
            for (int index = 0; index < 128; index++)
                File.WriteAllBytes(Path.Combine(root, "entry-" + index.ToString("D3") + ".bin"), new byte[] { 1, 2, 3, 4 });

            CancellationTokenSource source = new CancellationTokenSource();
            long lastProgress = 0;
            StorageAnalysisResult result = new StorageAnalysisService().FindLargeFiles(new[] { root }, 0, 256, source.Token, progress =>
            {
                lastProgress = progress.FilesVisited;
                if (progress.FilesVisited >= 64) source.Cancel();
            });

            Expect(result.WasCancelled, "Analise somente leitura precisa respeitar cancelamento no meio da arvore.");
            Expect(lastProgress >= 64, "O callback de progresso precisa ser chamado antes do cancelamento intermediario.");
            Expect(File.Exists(Path.Combine(root, "entry-000.bin")) && File.Exists(Path.Combine(root, "entry-127.bin")),
                "Cancelamento de analise nao pode remover arquivos da fixture.");
        }

        private static void TestJunctionGuard(string fixtureRoot)
        {
            string target = Path.Combine(fixtureRoot, "junction-target");
            string junction = Path.Combine(fixtureRoot, "junction-link");
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "outside-cache.bin"), "fixture");

            CreateJunction(junction, target);

            Expect(Directory.Exists(junction), "A fixture de junction precisa existir.");
            Expect(PathSafetyPolicy.ContainsReparsePoint(junction), "A politica precisa detectar a junction real.");

            Rule rule = CreateRule("fixture-junction-v1", RiskLevel.Safe);
            SafetyDecision decision = new PathSafetyPolicy().ValidateFinding(new Finding(null, rule, "T:\\", fixtureRoot, junction, null, 0, 0));
            Expect(!decision.Allowed, "Uma junction real precisa ser bloqueada antes de virar alvo de limpeza.");

            string toctouRoot = Path.Combine(fixtureRoot, "toctou-root");
            string plannedTarget = Path.Combine(toctouRoot, "cache");
            string redirectedTarget = Path.Combine(fixtureRoot, "toctou-external");
            Directory.CreateDirectory(plannedTarget);
            Directory.CreateDirectory(redirectedTarget);
            string externalFile = Path.Combine(redirectedTarget, "preserve.bin");
            File.WriteAllText(externalFile, "must remain");

            Finding plannedFinding = new Finding("fixture-toctou", rule, "T:\\", toctouRoot, plannedTarget, null, 0, 0);
            ActionPlan plan = ActionPlan.Create(CreateReviewedSession(plannedFinding), new[] { plannedFinding.FindingId });
            Directory.Delete(plannedTarget, true);
            CreateJunction(plannedTarget, redirectedTarget);

            CleanupReceipt receipt = new CleanupExecutor(new PathSafetyPolicy(), new FakeProcessInspector(false)).Execute(plan, CancellationToken.None, null);
            Expect(receipt.Results.Single().Status == ActionStatus.Blocked, "O executor precisa revalidar e bloquear alvo trocado por junction apos o plano.");
            Expect(File.Exists(externalFile), "O destino externo da junction precisa permanecer intacto.");
        }

        private static void TestDirectorySymlinkGuard(string fixtureRoot)
        {
            string target = Path.Combine(fixtureRoot, "symlink-target");
            string link = Path.Combine(fixtureRoot, "symlink-link");
            Directory.CreateDirectory(target);
            string externalFile = Path.Combine(target, "preserve.bin");
            File.WriteAllText(externalFile, "must remain outside a symbolic link");
            CreateDirectorySymbolicLink(link, target);

            Expect(Directory.Exists(link), "A fixture de symlink precisa existir.");
            Expect(PathSafetyPolicy.ContainsReparsePoint(link), "A politica precisa detectar o symlink real.");

            Rule rule = CreateRule("fixture-symlink-v1", RiskLevel.Safe);
            SafetyDecision decision = new PathSafetyPolicy().ValidateFinding(new Finding(null, rule, "T:\\", fixtureRoot, link, null, 0, 0));
            Expect(!decision.Allowed, "Um symlink de diretorio precisa ser bloqueado antes de virar alvo de limpeza.");
            Expect(File.Exists(externalFile), "A verificacao de symlink nao pode alterar o destino externo.");
        }

        private static void DeleteFixtureRoot(string fixtureRoot)
        {
            if (!Directory.Exists(fixtureRoot)) return;

            RemoveReparsePoints(fixtureRoot);
            if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, true);
        }

        private static void RemoveReparsePoints(string directory)
        {
            foreach (string item in Directory.GetFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(item);
                if ((attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    if (Directory.Exists(item)) RemoveJunction(item);
                    else File.Delete(item);
                }
                else if (Directory.Exists(item))
                {
                    RemoveReparsePoints(item);
                }
            }
        }

        private static void CreateJunction(string junction, string target)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = "/d /c mklink /J \"" + junction + "\" \"" + target + "\"";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Nao foi possivel criar a fixture de junction: " + process.StandardError.ReadToEnd());
            }
        }

        private static void CreateDirectorySymbolicLink(string link, string target)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = "/d /c mklink /D \"" + link + "\" \"" + target + "\"";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Nao foi possivel criar a fixture de symlink: " + process.StandardError.ReadToEnd());
            }
        }

        private static void RemoveJunction(string junction)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = "/d /c rmdir \"" + junction + "\"";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Nao foi possivel remover a junction de fixture: " + process.StandardError.ReadToEnd());
            }
        }

        private static ScanSession CreateReviewedSession(Finding finding)
        {
            ScanSession session = new ScanSession("Teste", "test");
            session.BeginScan();
            session.AddFinding(finding);
            session.CompleteScan();
            return session;
        }

        private static Rule CreateRule(string ruleId, RiskLevel risk)
        {
            return CreateRule(ruleId, risk, new string[0]);
        }

        private static Rule CreateRule(string ruleId, RiskLevel risk, IEnumerable<string> requiredProcesses)
        {
            return new Rule(ruleId, "1", "Fixture", "Regra de teste", risk, RuleActionKind.DirectoryContents,
                new[] { "Teste" }, requiredProcesses);
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class FakeProcessInspector : IProcessInspector
        {
            private readonly bool running;

            public FakeProcessInspector(bool running)
            {
                this.running = running;
            }

            public bool IsAnyRunning(IEnumerable<string> processNames, out string runningProcess)
            {
                runningProcess = running ? "fakeprocess" : string.Empty;
                return running && processNames.Any();
            }
        }
    }
}
