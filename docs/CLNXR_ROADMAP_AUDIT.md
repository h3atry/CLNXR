# CLNXR — Auditoria de execução do roadmap

Data da auditoria: 19 de agosto de 2026. Esta tabela é uma verificação de estado, não uma promessa de release.

## Requisitos transversais

| Requisito do roadmap | Estado | Evidência atual | Limite restante |
| --- | --- | --- | --- |
| Local-first, análise antes de limpar | Implementado | `Clnxr.Desktop`, `Clnxr.Platform.Windows`, testes e recibos locais; regras personalizadas também exigem prévia antes de persistir e limpar. | Falta ampliar validação visual para fluxos completos (não só startup smoke). |
| Não tocar navegador, login, cookies, Downloads, saves ou pessoal | Implementado no catálogo/política | `CLNXR_SAFETY_MODEL.md`, `PathSafetyPolicy.cs`, catálogo. | Requer auditoria externa antes de afirmar cobertura universal. |
| Não seguir reparse point nem hard link externo | Implementado e testado localmente | `PathSafetyPolicy.cs`; fixtures de junction real, troca TOCTOU, symlink de diretório e hard link físico externo em `Clnxr.Safety.Tests`. | Faltam concorrência de outros processos em VM. |
| Não matar processo nem elevar automaticamente | Implementado | `CleanupExecutor.cs`, inspetor de processos, UI de skips. | Faltam testes com processos reais de terceiros. |
| Evidência/recibo de ação | Implementado localmente | `ReceiptStore.cs`, hash SHA-256, testes de recibo normal, migração formal (v0->v1) e de ferramenta, histórico, visualizador estruturado somente leitura e exportação explícita de JSON na UI. | Falta reduzir lacunas de validação visual e de volume. |
| Nome/marca e publicação | Parcial — pré-release técnico | Nome em uso local limpo, sem marcação textual de rascunho em `README.md` e `MainForm`; o GitHub não mantém release ativo neste ciclo. | Exige decisão e validação jurídica/operacional, assinatura e canal de release estável. |

## Fases do roadmap

| Fase | Estado | Evidência | O que falta para marcar concluída |
| --- | --- | --- | --- |
| 0 — Auditoria | Concluída localmente | `CLNXR_CURRENT_ARCHITECTURE.md`, `CLNXR_TARGET_ARCHITECTURE.md`. | Nada de código; reauditar em cada mudança estrutural. |
| 1 — Fundação | Concluída localmente com ressalva de toolchain | Módulos `Core`, `Safety`, `Actions`, `Evidence`, `Platform.Windows` e `Application`; `CLNXR.sln` compilado em assemblies físicos e testes separados executados. O catálogo Windows é um pacote declarativo embutido (`clnxr.rules.windows.v1`) validado antes do uso, e existe verificação local de envelopes RSA/SHA-256. | SDK/targeting pack fixado e build sem avisos de referência/arquitetura; chave pública de produção e distribuição autenticada ainda não existem. |
| 2 — Shell/UI | Parcial forte | `src/Clnxr.Desktop` traz navegação, páginas, riscos, progressos, cancelamento, histórico, grade de resultados virtualizada e preferência local de movimento reduzido; `Clnxr.Desktop.Smoke` constrói a janela e verifica o contrato virtual sem exibi-la. | Teste visual, acessibilidade, DPI, benchmark de 60 FPS e validação em runtime; decisão WPF/WinUI depois de SDK. |
| 3 — Limpador seguro P0 | Parcial forte | Perfis Seguro/Completo/Jogos/Desenvolvedor/Personalizado, revalidação, filtro de idade para dumps/WER, caches GPU Intel/AMD/NVIDIA, caches de navegador restritos a subpastas conhecidas, bytes removidos, nova análise, fixtures e recibos redigidos. | Integração em VM para arquivos abertos e caches reais; Delivery Optimization/Windows Update ainda só abre a ferramenta oficial. |
| 4 — Resultados e histórico | Parcial forte | Grid de resultados virtualizado, riscos, regras, recibos listados localmente, verificação SHA-256, visualização estruturada em modo leitura e exportação explícita de JSON. | Benchmark de volume e validação visual/manual. |
| 5 — Ferramentas P1 | Parcial funcional | Perfis Jogos/Desenvolvedor em `REVIEW`; stores NuGet/npm/pnpm/Yarn/pip/uv e caches Gradle/Maven/Cargo; caches Discord/Teams/Epic/Spotify e caminho Electron delimitado; regras personalizadas locais com prévia e limpeza explícita; Lixeira isolada pela API oficial; launcher confirmado para o Storage Sense oficial; mapa de disco, arquivos grandes e duplicados em modo somente leitura com cancelamento e defesa contra reparse point; hard link externo protegido; Explorador de inicialização com desabilitação/restauração reversível somente em HKCU; Inspetor de arquivos bloqueados; inventário de resíduos baseado em `InstallLocation`; agendamento Seguro opt-in com argumentos fixos, recibo e remoção explícita. | Teste visual, desempenho em volumes grandes, esvaziamento em VM, teste manual controlado de Registro/Task Scheduler, chave/canal de regras assinadas e cobertura maior de launchers/dev tools. |
| 5 — P3 local: Network Utilities e System Repair Hub | Implementado localmente com limites | Diagnóstico/Flush DNS, planos de reset sem execução automática, SFC/DISM/CHKDSK somente verificação, UI confirmável, comandos fixos, saída limitada e recibos locais; contratos cobertos no grupo P3. | Falta validação visual e em VM/ACL/conta elevada; não há promessa de reparo, disponibilidade de utilitário ou resultado universal. |
| 6 — Beta público | Pré-release técnico, sem release ativo | O fluxo local usa `artifacts\CLNXR-Portable-SingleFile\CLNXR.exe` como único artefato de distribuição interna. Não há release público ativo após a limpeza de pré-lançamentos. | Assinatura, política/termos revisados, updater, VM limpa, auditoria e beta consentido. |
| 7 — 1.0 | Não iniciado | Nenhuma evidência suficiente. | Todos os gates de beta, telemetria/relato opcional, suporte e auditoria externa. |
| 8 — Futuro | Fora do P0/P1 | Nenhuma ação requerida agora. | Só priorizar após dados reais de beta. |

## Evidência de validação local disponível

- `Clnxr.Safety.Tests`: vinte e um grupos executados em sandbox temporário; inclui destino protegido, limpeza pelo serviço de aplicação, recibos com redação de caminho, cancelamento antes e durante análise P1, arquivo bloqueado, catálogo P0/P1, validação do pacote declarativo e perfil Personalizado, verificação de envelope assinado com payload adulterado/chave incorreta, regras personalizadas com prévia/limpeza explícita e persistência local, caches de navegador protegidos, filtro de idade, ferramentas P1 somente leitura, P2 reversível, contratos fechados de Network Utilities/System Repair, preferências locais sanitizadas, negação de acesso em arquivo de teste, validação de migração de recibos (`clnxr.receipt.v0` -> `clnxr.receipt.v1`) e remoção delimitada dos dados próprios do CLNXR, junction, TOCTOU, symlink de diretório e hard link externo preservado.
- `Clnxr.WindowsScan.Smoke`: scan Seguro real em modo leitura e consulta global da Lixeira pela API do Windows, sem alteração de dados.
- `Clnxr.Desktop.Smoke`: construção e descarte não visual da janela desktop em STA e verificação de `DataGridView.VirtualMode` na página Resultados, sem análise ou limpeza.
- `CLNXR.sln`: build Release dos doze projetos pelo MSBuild clássico e execução dos quatro binários de teste contra os assemblies físicos; o CLI também foi exercitado com dry-run e JSON.
- `artifacts\CLNXR-Portable-SingleFile\CLNXR.exe`: artefato único para uso local de teste e compartilhamento técnico.
- `visual smoke`: execução em diretório temporário confirmou janela e captura visual (`C:\Users\askovski\AppData\Local\Temp\clnxr-portable-smoke-ui\clnxr-ui-smoke.png`) sem falha de inicialização.
- O pacote da revisão atual deve ser validado quanto aos arquivos obrigatórios, hashes internos e dry-run do CLI extraído antes da publicação; os hashes de dev.3 permanecem apenas como histórico.
- Validação de desempenho P1 em carga sintética: `tmp\\Clnxr.StorageAnalysisPerfSmoke.cs` concluíu `DiskMapElapsedMs=3751`, `LargeElapsedMs=3567`, `DuplicatesElapsedMs=3317` em `9.060` arquivos (3.000 por bucket), com `LargeIssues=1`, `DuplicateGroups=1` e 1 grupo de duplicados (`46.399.488` bytes recuperáveis).

## Auditoria de artefatos — 19/08/2026

| Verificação | Resultado | Interpretação correta |
| --- | --- | --- |
| Bundle portátil (único de distribuição) | Presente | `artifacts\CLNXR-Portable-SingleFile\CLNXR.exe` é o artefato operacional para este ciclo; não prova prontidão pública por si só. |
| SHA-256 do executável único | `31AB96513CD0DD966FA8DAB4FC71DFC462EEB3E0ADC95C8044AC162524723BF3` | `artifacts\\CLNXR-Portable-SingleFile\\CLNXR.exe` foi recompilado sem dependência de `Clnxr.*.dll` no diretório e com validação de `Assembly.GetReferencedAssemblies`. |
| SBOM | `SPDX-2.3` válido | Inventário de desenvolvimento, ainda sem revisão de distribuição. |
| Assinatura Authenticode | `NotSigned` | Gate de beta público e 1.0 não atendido. |
| SDK .NET detectado | Não | O ambiente atual não permite recompilação independente por SDK; a build existente não prova reprodutibilidade. |
| Estado Git | `master` com commits locais/repositório remoto e sem release técnico ativo | Commit remoto verificado: `e4ab46d76a9cc88f7bc5f6844bcf46ed6693b619`; os pacotes `v0.1.0-dev.5` a `v0.1.0-dev.8` em formato ZIP foram removidos do repositório de trabalho. |

## Decisão de término honesta

O desenvolvimento local atingiu um protótipo P0/P1/P3 com validações específicas. O roadmap completo não pode ser declarado terminado porque as fases 6 e 7 exigem autoridade e estado externo que não podem ser simulados por código: identidade/marca, certificado de assinatura, ambiente limpo, publicação controlada, atualização, beta externo e revisão jurídica/segurança.

O próximo trabalho local legítimo é reduzir as lacunas de teste e de UI listadas acima. Para ultrapassar os gates externos, é necessária autorização e os respectivos recursos; não é aceitável substituir esses gates por um print, checksum ou compilação local.


