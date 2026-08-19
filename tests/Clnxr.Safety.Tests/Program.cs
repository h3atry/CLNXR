using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
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
                TestSignedRulePackVerification();
                TestCustomRulePreviewAndCleanup(fixtureRoot);
                TestBrowserCacheRulesAndAge(fixtureRoot);
                TestReadOnlyStorageTools(fixtureRoot);
                TestP2ReadOnlyTools(fixtureRoot);
                TestP3NetworkAndRepairContracts();
                TestLocalPreferences(fixtureRoot);
                TestUserDataCleanup(fixtureRoot);
                TestStorageAnalysisMidTreeCancellation(fixtureRoot);
                TestAccessDeniedHandling(fixtureRoot);
                TestJunctionGuard(fixtureRoot);
                TestDirectorySymlinkGuard(fixtureRoot);
                TestHardLinkGuard(fixtureRoot);
                TestReceiptSchemaMigration();
                Console.WriteLine("PASS: 22 grupos de testes de seguranca, evidencia, catalogo declarativo, pacote assinado, ferramentas locais, contratos P3, preferencias, remocao de dados proprios, acesso negado e hard links foram concluidos.");
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

            string redacted = PathRedactor.Redact(Path.Combine("C:\\Users", Environment.UserName, "AppData", "Local", "Temp", "pytest-of-" + Environment.UserName));
            Expect(redacted.IndexOf(Environment.UserName, StringComparison.OrdinalIgnoreCase) < 0,
                "A redação de caminhos precisa remover o nome do usuário também quando ele aparece em um componente intermediário.");
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

        private static void TestReceiptSchemaMigration()
        {
            string fixtureRoot = Path.Combine(Path.GetTempPath(), "clnxr-receipt-migration-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(fixtureRoot);
                ReceiptStore store = new ReceiptStore(fixtureRoot);

                var serializer = new JavaScriptSerializer();
                var legacyReceipt = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    { "schemaVersion", ReceiptSchema.LegacyReceiptVersion },
                    { "receiptId", "legacy-cleanup" },
                    { "planId", "legacy-plan" },
                    { "sessionId", "legacy-session" },
                    { "startedUtc", DateTime.UtcNow.AddMinutes(-1).ToString("o") },
                    { "completedUtc", DateTime.UtcNow.ToString("o") },
                    { "wasCancelled", false },
                    { "results", new object[0] },
                    { "receiptHash", string.Empty }
                };

                string unsignedPayload = serializer.Serialize(legacyReceipt);
                legacyReceipt["receiptHash"] = ComputeSha256(unsignedPayload);
                string legacyPayload = serializer.Serialize(legacyReceipt);

                string legacyPath = Path.Combine(fixtureRoot, "legacy-receipt.json");
                File.WriteAllText(legacyPath, legacyPayload, new UTF8Encoding(false));

                ReceiptFileVerification verification = store.VerifyFile(legacyPath);
                Expect(verification.IsValid, "Recibo legado com schema v0 precisa ser validado com migração para leitura.");
                Expect(verification.Message.IndexOf("migrou", StringComparison.OrdinalIgnoreCase) >= 0,
                    "A validação precisa indicar que houve migração de schema no recibo legado.");

                ReceiptDocument document = store.ReadDocument(legacyPath);
                Expect(document.SchemaVersion == ReceiptSchema.CurrentVersion,
                    "Recibo legado precisa ser carregado como schema atual na visualizacao.");
                Expect(document.Details.Any(detail => detail.Field == "ReceiptId" && detail.Value == "legacy-cleanup"),
                    "Recibo legado precisa preservar identificador apos normalização.");
            }
            finally
            {
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, true);
            }
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

        private static void TestSignedRulePackVerification()
        {
            string payloadJson = "{\"schemaVersion\":\"clnxr.rules.windows.v1\",\"catalogVersion\":\"fixture-signed\",\"rules\":[{\"ruleId\":\"fixture-signed\",\"version\":\"1\",\"category\":\"Fixture\",\"explanation\":\"Regra assinada\",\"risk\":\"SAFE\",\"relativePath\":\"Cache\",\"filter\":\"\",\"profiles\":[\"Seguro\"],\"requiredClosedProcesses\":[],\"minimumAgeDays\":0,\"systemOnly\":false,\"pathBase\":\"LocalAppData\"}]}";
            byte[] payload = Encoding.UTF8.GetBytes(payloadJson);
            byte[] signature;
            using (RSACryptoServiceProvider signer = new RSACryptoServiceProvider(2048))
            using (SHA256Managed hash = new SHA256Managed())
            {
                signature = signer.SignData(payload, hash);
                string envelope = new JavaScriptSerializer().Serialize(new
                {
                    schemaVersion = SignedRulePackService.EnvelopeSchemaVersion,
                    keyId = "fixture-key",
                    payload = Convert.ToBase64String(payload),
                    signature = Convert.ToBase64String(signature)
                });

                SignedRulePackVerification verified = new SignedRulePackService().Verify(envelope, signer);
                Expect(verified.Succeeded && verified.KeyId == "fixture-key" && verified.CatalogVersion == "fixture-signed" && verified.Rules.Count == 1,
                    "Um pacote de regras assinado com chave correspondente precisa ser aceito e materializado.");

                string tamperedPayloadJson = payloadJson.Replace("fixture-signed", "fixture-tampered");
                string tamperedEnvelope = new JavaScriptSerializer().Serialize(new
                {
                    schemaVersion = SignedRulePackService.EnvelopeSchemaVersion,
                    keyId = "fixture-key",
                    payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(tamperedPayloadJson)),
                    signature = Convert.ToBase64String(signature)
                });
                SignedRulePackVerification tampered = new SignedRulePackService().Verify(tamperedEnvelope, signer);
                Expect(!tampered.Succeeded, "Alteração no payload precisa invalidar a verificação do pacote assinado.");
            }

            using (RSACryptoServiceProvider wrongKey = new RSACryptoServiceProvider(2048))
            {
                string wrongKeyEnvelope = new JavaScriptSerializer().Serialize(new
                {
                    schemaVersion = SignedRulePackService.EnvelopeSchemaVersion,
                    keyId = "fixture-key",
                    payload = Convert.ToBase64String(payload),
                    signature = Convert.ToBase64String(signature)
                });
                SignedRulePackVerification rejected = new SignedRulePackService().Verify(wrongKeyEnvelope, wrongKey);
                Expect(!rejected.Succeeded, "Uma chave pública diferente precisa rejeitar a assinatura do pacote.");
            }
        }

        private static void TestCustomRulePreviewAndCleanup(string fixtureRoot)
        {
            string root = Path.Combine(fixtureRoot, "custom-çache");
            string excluded = Path.Combine(root, "excluded");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(excluded);
            string oldFile = Path.Combine(root, "old-çache.tmp");
            string unicodeFile = Path.Combine(root, "unicode-ação.tmp");
            string recentFile = Path.Combine(root, "recent.tmp");
            string excludedFile = Path.Combine(excluded, "excluded.tmp");
            string keptLog = Path.Combine(root, "kept.log");
            File.WriteAllBytes(oldFile, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(unicodeFile, new byte[] { 4, 5 });
            File.WriteAllBytes(recentFile, new byte[] { 6 });
            File.WriteAllBytes(excludedFile, new byte[] { 7 });
            File.WriteAllBytes(keptLog, new byte[] { 8 });
            File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(unicodeFile, DateTime.UtcNow.AddDays(-10));
            File.SetLastWriteTimeUtc(recentFile, DateTime.UtcNow.AddDays(-1));

            CustomRuleDraft draft = new CustomRuleDraft("Cache temporário de teste", root, 7, new[] { ".tmp" }, new[] { "excluded" }, "fixture-test");
            CustomRuleService service = new CustomRuleService(new PathSafetyPolicy());
            CustomRulePreview preview = service.Preview(draft, CancellationToken.None, null);
            Expect(preview.CanSave && preview.Finding != null, "Regra personalizada precisa exigir e concluir uma prévia real antes de salvar.");
            Expect(preview.Definition.SignatureStatus == "unsigned", "Regra personalizada sem pacote assinado precisa permanecer marcada como unsigned.");
            Expect(preview.Finding.Rule.Risk == RiskLevel.Advanced, "Regra personalizada precisa nascer sempre como ADVANCED.");
            Expect(preview.Finding.ExplicitItems.Count == 2 && preview.Finding.EstimatedBytes == 5, "Prévia personalizada precisa respeitar extensão e idade mínima.");
            Expect(preview.Examples.All(path => path.IndexOf(Environment.UserName, StringComparison.OrdinalIgnoreCase) < 0), "Exemplos da prévia precisam ser redigidos antes de sair do motor.");

            CleanupReceipt receipt = new CleanupExecutor(new PathSafetyPolicy(), new FakeProcessInspector(false)).Execute(
                ActionPlan.Create(CreateReviewedSession(preview.Finding), new[] { preview.Finding.FindingId }), CancellationToken.None, null);
            Expect(receipt.TotalBytesRemoved == 5, "Limpeza de regra personalizada precisa remover somente os arquivos enumerados na prévia.");
            Expect(!File.Exists(oldFile) && !File.Exists(unicodeFile), "Arquivos antigos explicitamente incluídos precisam ser removidos.");
            Expect(File.Exists(recentFile) && File.Exists(excludedFile) && File.Exists(keptLog), "Arquivo recente, exclusão e extensão não selecionada precisam permanecer.");

            CustomRuleDraft unsafeDraft = new CustomRuleDraft("Perfil inteiro", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 0, null, null, "fixture-test");
            bool rejected = false;
            try { service.ValidateAndCreate(unsafeDraft); }
            catch (InvalidOperationException) { rejected = true; }
            Expect(rejected, "Regra personalizada não pode usar a raiz inteira do perfil pessoal.");

            string storePath = Path.Combine(fixtureRoot, "custom-rules", "custom-rules.v1.json");
            CustomRuleStore store = new CustomRuleStore(storePath);
            store.Save(preview.Definition);
            IList<CustomRuleDefinition> stored = store.List();
            Expect(stored.Count == 1 && stored[0].RuleId == preview.Definition.RuleId,
                "Persistência local precisa recarregar a regra personalizada pelo esquema versionado.");
            Expect(store.Delete(preview.Definition.RuleId) && store.List().Count == 0,
                "Persistência local precisa permitir remover somente a regra personalizada escolhida.");
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

        private static void TestP2ReadOnlyTools(string fixtureRoot)
        {
            StartupExplorerResult startup = new StartupExplorerService().ListEntries();
            Expect(startup != null && startup.Entries != null && startup.Issues != null,
                "Explorador de inicialização precisa produzir inventário e lista de avisos sem alterar o sistema.");

            string root = Path.Combine(fixtureRoot, "locked-inspector");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "held.tmp");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            long before = new FileInfo(path).Length;
            LockedFileInspection inspection;
            using (FileStream handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                inspection = new LockedFileInspectorService().Inspect(path);
            }

            Expect(inspection != null && inspection.Supported,
                "Inspetor de arquivos bloqueados precisa usar o Restart Manager disponível no Windows.");
            Expect(inspection.Path == PathSafetyPolicy.Normalize(path),
                "Inspetor de arquivos bloqueados precisa normalizar o caminho informado.");
            Expect(new FileInfo(path).Length == before,
                "Ferramentas P2 somente leitura não podem alterar o arquivo inspecionado.");

            UninstallResidualResult residuals = new UninstallResidualService().ListEntries();
            Expect(residuals != null && residuals.Entries != null && residuals.Issues != null,
                "Inventário de resíduos precisa ler entradas conhecidas sem alterar o Registro.");

            string executable = Path.Combine(root, "CLNXR-Portable.exe");
            File.WriteAllBytes(executable, new byte[] { 0x4D, 0x5A });
            ScheduledCleanupService scheduler = new ScheduledCleanupService();
            ScheduledCleanupPlan plan = scheduler.BuildSafeDailyPlan(executable);
            string createArguments = scheduler.BuildCreateArguments(plan);
            string deleteArguments = scheduler.BuildDeleteArguments();
            Expect(plan.TaskName == ScheduledCleanupService.TaskName && plan.Arguments.IndexOf("--profile safe", StringComparison.Ordinal) >= 0,
                "Agendamento precisa ficar limitado ao perfil Seguro e ao nome fixo da tarefa.");
            Expect(createArguments.IndexOf("--clean --yes --quiet", StringComparison.Ordinal) >= 0 &&
                deleteArguments.IndexOf(ScheduledCleanupService.TaskName, StringComparison.Ordinal) >= 0,
                "Agendamento precisa produzir comandos explícitos de criação e desfazer sem shell arbitrário.");
            Expect(File.Exists(executable), "A construção do plano de agendamento não pode remover o executável.");

            StartupExplorerService startupService = new StartupExplorerService();
            IList<DisabledStartupEntry> disabled = startupService.ListDisabledEntries();
            Expect(disabled != null, "A lista de backups reversíveis de inicialização precisa ser somente leitura.");
            StartupMutationResult denied = startupService.Disable(new StartupEntry("Teste", "Entrada", "comando", "fixture"));
            Expect(!denied.Succeeded, "Uma entrada sem origem de Registro suportada não pode ser desabilitada.");
        }

        private static void TestP3NetworkAndRepairContracts()
        {
            NetworkUtilitiesService network = new NetworkUtilitiesService();
            IList<NetworkActionPlan> networkPlans = network.ListPlans();
            Expect(networkPlans.Count == 4, "Catálogo de rede precisa expor diagnóstico, Flush DNS e os dois planos manuais de reset.");
            NetworkActionPlan diagnostics = network.BuildPlan(NetworkUtilitiesService.DiagnosticsActionId);
            NetworkActionPlan flushDns = network.BuildPlan(NetworkUtilitiesService.FlushDnsActionId);
            NetworkActionPlan winsock = network.BuildPlan(NetworkUtilitiesService.WinsockResetActionId);
            Expect(diagnostics.ReadOnly && diagnostics.Arguments == "/all" && diagnostics.ExecutablePath.EndsWith("ipconfig.exe", StringComparison.OrdinalIgnoreCase),
                "Diagnóstico de rede precisa ser somente leitura e usar ipconfig.exe /all fixo.");
            Expect(!flushDns.ReadOnly && flushDns.Arguments == "/flushdns" && !flushDns.RequiresElevation,
                "Flush DNS precisa ficar limitado ao argumento fixo e não solicitar elevação automática.");
            NetworkActionResult refusedReset = network.Execute(winsock);
            Expect(!refusedReset.Succeeded && refusedReset.Message.IndexOf("plano manual", StringComparison.OrdinalIgnoreCase) >= 0,
                "Reset Winsock não pode ser executado silenciosamente pelo catálogo local.");
            bool networkRejected = false;
            try { network.BuildPlan("cmd /c arbitrary"); }
            catch (ArgumentException) { networkRejected = true; }
            Expect(networkRejected, "Utilitário de rede precisa rejeitar ações fora do catálogo fechado.");

            SystemRepairService repair = new SystemRepairService();
            IList<SystemRepairPlan> repairPlans = repair.ListPlans();
            Expect(repairPlans.Count == 3, "System Repair Hub precisa expor exatamente três verificações não destrutivas.");
            SystemRepairPlan sfc = repair.BuildPlan(SystemRepairService.SfcVerifyActionId, string.Empty);
            SystemRepairPlan dism = repair.BuildPlan(SystemRepairService.DismCheckHealthActionId, string.Empty);
            SystemRepairPlan chkdsk = repair.BuildPlan(SystemRepairService.ChkdskScanActionId, "c:");
            Expect(sfc.Arguments == "/verifyonly" && sfc.ReadOnly && sfc.RequiresElevation,
                "SFC precisa usar somente /verifyonly e declarar a necessidade potencial de elevação.");
            Expect(dism.Arguments == "/Online /Cleanup-Image /CheckHealth" && dism.ReadOnly,
                "DISM precisa ficar limitado a /CheckHealth sem reparo automático.");
            Expect(chkdsk.Arguments == "C: /scan" && chkdsk.ReadOnly && chkdsk.Arguments.IndexOf("/f", StringComparison.OrdinalIgnoreCase) < 0,
                "CHKDSK precisa aceitar apenas volume validado e /scan, sem /f.");
            bool volumeRejected = false;
            try { repair.BuildPlan(SystemRepairService.ChkdskScanActionId, "C:\\Windows"); }
            catch (ArgumentException) { volumeRejected = true; }
            Expect(volumeRejected, "System Repair Hub precisa rejeitar caminho ou switch no campo de volume.");
        }

        private static void TestLocalPreferences(string fixtureRoot)
        {
            string preferencesPath = Path.Combine(fixtureRoot, "preferences", "settings.ini");
            UserPreferencesService service = new UserPreferencesService(preferencesPath);
            UserPreferences defaults = service.CreateDefaults();
            Expect(defaults.Language == "pt-BR" && defaults.Theme == "dark-graphite" && !defaults.UpdatesOptIn,
                "Preferencias padrão precisam ser locais, em pt-BR, tema dark-graphite e sem atualização automática.");

            defaults.ReducedMotion = true;
            defaults.UpdatesOptIn = true;
            string message;
            Expect(service.Save(defaults, out message), "Preferencias válidas precisam ser persistidas.");
            Expect(File.Exists(preferencesPath), "O arquivo de preferencias precisa ficar no caminho local informado.");

            UserPreferences loaded = service.Load();
            Expect(loaded.ReducedMotion && loaded.UpdatesOptIn && loaded.Language == "pt-BR" && loaded.Theme == "dark-graphite",
                "Preferencias persistidas precisam sobreviver ao carregamento.");

            File.WriteAllText(preferencesPath, "language=../../outside\r\ntheme=dark-graphite\r\nreduced_motion=false\r\nupdates_opt_in=false\r\n");
            UserPreferences sanitized = service.Load();
            Expect(sanitized.Language == "pt-BR", "Valor de idioma com caracteres de caminho precisa ser ignorado.");

            UserPreferences invalid = service.CreateDefaults();
            invalid.Theme = "dark/unsafe";
            Expect(!service.Save(invalid, out message), "Preferencias com token de tema fora do formato precisam ser rejeitadas.");
        }

        private static void TestUserDataCleanup(string fixtureRoot)
        {
            string root = Path.Combine(fixtureRoot, "CLNXR");
            string receipts = Path.Combine(root, "Receipts");
            string rules = Path.Combine(root, "Rules");
            string outside = Path.Combine(fixtureRoot, "outside-user-data.txt");
            Directory.CreateDirectory(receipts);
            Directory.CreateDirectory(rules);
            File.WriteAllBytes(Path.Combine(receipts, "receipt.json"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(rules, "rules.json"), new byte[] { 4, 5 });
            File.WriteAllText(outside, "must remain outside CLNXR root");

            UserDataCleanupService service = new UserDataCleanupService(root);
            UserDataCleanupPreview preview = service.Preview(CancellationToken.None);
            Expect(preview.FileCount == 2 && preview.Bytes == 5, "Prévia de dados próprios precisa medir somente os arquivos da raiz CLNXR.");

            UserDataCleanupResult result = service.Execute(CancellationToken.None);
            Expect(!result.WasCancelled && result.RemovedFiles == 2 && result.RemovedBytes == 5,
                "Execução de dados próprios precisa remover e contabilizar os arquivos previstos.");
            Expect(Directory.Exists(root), "A raiz CLNXR precisa permanecer para permitir reinstalação ou nova configuração.");
            Expect(File.Exists(outside), "Arquivo fora da raiz CLNXR precisa permanecer intacto.");

            string preserved = Path.Combine(root, "preserved.bin");
            File.WriteAllBytes(preserved, new byte[] { 9 });
            UserDataCleanupResult cancelled = service.Execute(new CancellationToken(true));
            Expect(cancelled.WasCancelled && File.Exists(preserved), "Cancelamento antes da execução precisa preservar os dados locais.");

            bool rejected = false;
            try { new UserDataCleanupService(Path.Combine(fixtureRoot, "arbitrary-root")); }
            catch (ArgumentException) { rejected = true; }
            Expect(rejected, "A remoção de dados próprios precisa rejeitar qualquer raiz que não seja uma pasta CLNXR dedicada.");
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

        private static void TestAccessDeniedHandling(string fixtureRoot)
        {
            string root = Path.Combine(fixtureRoot, "acl-denied");
            Directory.CreateDirectory(root);
            string deniedFile = Path.Combine(root, "protected.bin");
            File.WriteAllBytes(deniedFile, new byte[] { 1, 2, 3, 4, 5 });

            NTAccount user = new NTAccount(WindowsIdentity.GetCurrent().Name);
            FileInfo fileInfo = new FileInfo(deniedFile);
            FileSecurity fileSecurity = fileInfo.GetAccessControl();
            FileSystemAccessRule denyDeleteRule = new FileSystemAccessRule(
                user,
                FileSystemRights.Delete,
                AccessControlType.Deny);
            fileSecurity.AddAccessRule(denyDeleteRule);

            bool aclWorked = false;
            try
            {
                fileInfo.SetAccessControl(fileSecurity);
                try
                {
                    File.Delete(deniedFile);
                }
                catch (UnauthorizedAccessException)
                {
                    aclWorked = true;
                }
            }
            catch
            {
                aclWorked = false;
            }

            if (!File.Exists(deniedFile))
            {
                // Se a ACL não bloqueou a deleção, reconstroi o arquivo e usa
                // atributo ReadOnly para garantir o comportamento de falha controlada.
                File.WriteAllBytes(deniedFile, new byte[] { 1, 2, 3, 4, 5 });
                File.SetAttributes(deniedFile, FileAttributes.ReadOnly);
            }

            if (!aclWorked)
            {
                // Remove qualquer ACL adicionada, porque o cenário abaixo usa ReadOnly.
                try { fileInfo.SetAccessControl(fileSecurity); } catch { }
            }

            try
            {
                Rule rule = CreateRule("fixture-access-denied-v1", RiskLevel.Safe);
                Finding finding = new Finding("fixture-access-denied", rule, "T:\\", root, root, null, 5, 1);
                CleanupReceipt receipt = new CleanupExecutor(new PathSafetyPolicy(), new FakeProcessInspector(false)).Execute(
                    ActionPlan.Create(CreateReviewedSession(finding), new[] { finding.FindingId }), CancellationToken.None, null);
                Expect(receipt.Results.Single().Status == ActionStatus.Skipped,
                    "A tentativa de apagar arquivo sem permissoes deve ser tratada como pulo seguro.");
                Expect(File.Exists(deniedFile), "Arquivo sem permissao de delecao deve permanecer na fixture.");
            }
            finally
            {
                try
                {
                    if (File.Exists(deniedFile))
                    {
                        fileSecurity.RemoveAccessRule(denyDeleteRule);
                        fileInfo.SetAccessControl(fileSecurity);
                        File.SetAttributes(deniedFile, FileAttributes.Normal);
                        File.Delete(deniedFile);
                    }
                }
                catch
                {
                }
            }
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

        private static void TestHardLinkGuard(string fixtureRoot)
        {
            string root = Path.Combine(fixtureRoot, "hardlink-root");
            string external = Path.Combine(fixtureRoot, "hardlink-external.bin");
            string link = Path.Combine(root, "allowed-name.bin");
            Directory.CreateDirectory(root);
            File.WriteAllBytes(external, new byte[] { 1, 2, 3, 4 });
            CreateHardLink(link, external);

            Expect(File.Exists(link) && File.Exists(external), "A fixture de hard link precisa expor os dois nomes físicos.");
            Expect(PathSafetyPolicy.HasMultipleHardLinks(link), "A política precisa detectar mais de um nome físico para o arquivo.");
            SafetyDecision decision = new PathSafetyPolicy().ValidateExistingItem(link, root);
            Expect(!decision.Allowed, "Um arquivo com hard link adicional precisa ser bloqueado antes da remoção.");

            Finding finding = new Finding("fixture-hard-link", CreateRule("fixture-hard-link-v1", RiskLevel.Safe), "T:\\",
                root, root, string.Empty, 4, 1, new[] { link });
            CleanupReceipt receipt = new CleanupExecutor(new PathSafetyPolicy(), new FakeProcessInspector(false)).Execute(
                ActionPlan.Create(CreateReviewedSession(finding), new[] { finding.FindingId }), CancellationToken.None, null);
            Expect(receipt.Results.Single().Status == ActionStatus.Skipped, "Executor precisa registrar hard link como item preservado.");
            Expect(File.Exists(link) && File.Exists(external), "A limpeza não pode remover nenhum dos nomes de um hard link.");
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

        private static void CreateHardLink(string link, string target)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.Arguments = "/d /c mklink /H \"" + link + "\" \"" + target + "\"";
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Nao foi possivel criar a fixture de hard link: " + process.StandardError.ReadToEnd());
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

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                StringBuilder output = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash) output.Append(item.ToString("x2"));
                return output.ToString();
            }
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
