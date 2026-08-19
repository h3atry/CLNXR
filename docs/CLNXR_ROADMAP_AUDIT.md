# CLNXR — Auditoria de execução do roadmap

Data da auditoria: 18 de agosto de 2026. Esta tabela é uma verificação de estado, não uma promessa de release.

## Requisitos transversais

| Requisito do roadmap | Estado | Evidência atual | Limite restante |
| --- | --- | --- | --- |
| Local-first, análise antes de limpar | Implementado | `Clnxr.Desktop`, `Clnxr.Platform.Windows`, testes e recibos locais; regras personalizadas também exigem prévia antes de persistir e limpar. | Falta teste visual manual. |
| Não tocar navegador, login, cookies, Downloads, saves ou pessoal | Implementado no catálogo/política | `CLNXR_SAFETY_MODEL.md`, `PathSafetyPolicy.cs`, catálogo. | Requer auditoria externa antes de afirmar cobertura universal. |
| Não seguir reparse point | Implementado e testado | `PathSafetyPolicy.cs`; fixtures de junction real, troca TOCTOU e symlink de diretório real em `Clnxr.Safety.Tests`. | Faltam combinações de hard link/ACL/concorrência de outros processos. |
| Não matar processo nem elevar automaticamente | Implementado | `CleanupExecutor.cs`, inspetor de processos, UI de skips. | Faltam testes com processos reais de terceiros. |
| Evidência/recibo de ação | Implementado localmente | `ReceiptStore.cs`, hash SHA-256, testes de recibo normal e de ferramenta, histórico, visualizador estruturado somente leitura e exportação explícita de JSON na UI. | Falta esquema formal de migração. |
| Nome/marca e publicação | Parcial — pré-release técnico | Nome ainda é provisório em `README.md`; repositório público `h3atry/CLNXR` e release pré-lançamento foram publicados após esta auditoria local. | Exige decisão e validação jurídica/operacional, assinatura e canal de release estável. |

## Fases do roadmap

| Fase | Estado | Evidência | O que falta para marcar concluída |
| --- | --- | --- | --- |
| 0 — Auditoria | Concluída localmente | `CLNXR_CURRENT_ARCHITECTURE.md`, `CLNXR_TARGET_ARCHITECTURE.md`. | Nada de código; reauditar em cada mudança estrutural. |
| 1 — Fundação | Concluída localmente com ressalva de toolchain | Módulos `Core`, `Safety`, `Actions`, `Evidence`, `Platform.Windows` e `Application`; `CLNXR.sln` compilado em assemblies físicos e testes separados executados. O catálogo Windows agora é um pacote declarativo embutido (`clnxr.rules.windows.v1`) validado antes do uso. | SDK/targeting pack fixado e build sem avisos de referência/arquitetura; assinatura do pacote de regras ainda não existe. |
| 2 — Shell/UI | Parcial forte | `src/Clnxr.Desktop` traz navegação, páginas, riscos, progressos, cancelamento e histórico; `Clnxr.Desktop.Smoke` constrói a janela sem exibi-la. | Teste visual, acessibilidade, DPI e movimento reduzido em runtime; decisão WPF/WinUI depois de SDK. |
| 3 — Limpador seguro P0 | Parcial forte | Perfis Seguro/Completo/Jogos/Desenvolvedor/Personalizado, revalidação, filtro de idade para dumps/WER, caches GPU Intel/AMD/NVIDIA, caches de navegador restritos a subpastas conhecidas, bytes removidos, nova análise, fixtures e recibos redigidos. | Integração em VM para ACL, arquivos abertos e caches reais; Delivery Optimization/Windows Update ainda só abre a ferramenta oficial. |
| 4 — Resultados e histórico | Parcial forte | Grid de resultados, riscos, regras, recibos listados localmente, verificação SHA-256, visualização estruturada em modo leitura e exportação explícita de JSON. | Filtros/virtualização e validação visual/manual do fluxo. |
| 5 — Ferramentas P1 | Parcial funcional | Perfis Jogos/Desenvolvedor em `REVIEW`; stores NuGet/npm/pnpm/Yarn/pip/uv e caches Gradle/Maven/Cargo; caches Discord/Teams/Epic/Spotify e caminho Electron delimitado; regras personalizadas locais com prévia e limpeza explícita; Lixeira isolada pela API oficial; launcher confirmado para o Storage Sense oficial; mapa de disco, arquivos grandes e duplicados em modo somente leitura com cancelamento e defesa contra reparse point; Explorador de inicialização e Inspetor de arquivos bloqueados integrados à UI como consultas somente leitura. | Teste visual, desempenho em volumes grandes, esvaziamento em VM, importação/assinatura de regras e cobertura maior de launchers/dev tools. |
| 6 — Beta público | Pré-release técnico, não beta | Pacote ZIP, checksum e SBOM de desenvolvimento publicados em `https://github.com/h3atry/CLNXR/releases`. | Assinatura, política/termos revisados, updater, VM limpa, auditoria e beta consentido. |
| 7 — 1.0 | Não iniciado | Nenhuma evidência suficiente. | Todos os gates de beta, telemetria/relato opcional, suporte e auditoria externa. |
| 8 — Futuro | Fora do P0/P1 | Nenhuma ação requerida agora. | Só priorizar após dados reais de beta. |

## Evidência de validação local disponível

- `Clnxr.Safety.Tests`: treze grupos executados em sandbox temporário; inclui destino protegido, limpeza pelo serviço de aplicação, recibos com redação de caminho, cancelamento antes e durante análise P1, arquivo bloqueado, catálogo P0/P1, validação do pacote declarativo e perfil Personalizado, regras personalizadas com prévia/limpeza explícita e persistência local, caches de navegador protegidos, filtro de idade, ferramentas P1 somente leitura, Explorador de inicialização, Inspetor de arquivos bloqueados via Restart Manager, junction, TOCTOU e symlink de diretório.
- `Clnxr.WindowsScan.Smoke`: scan Seguro real em modo leitura e consulta global da Lixeira pela API do Windows, sem alteração de dados.
- `Clnxr.Desktop.Smoke`: construção e descarte não visual da janela desktop em STA, sem análise ou limpeza.
- `CLNXR.sln`: build Release dos doze projetos pelo MSBuild clássico e execução dos quatro binários de teste contra os assemblies físicos; o CLI também foi exercitado com dry-run e JSON.
- `artifacts\CLNXR-Portable\`: bundle histórico; a revisão validada atual é o ZIP `artifacts\CLNXR-Portable-0.1.0-dev.3.zip` com 16 entradas sob `CLNXR-Portable/`.
- `artifacts\CLNXR-Portable-0.1.0-dev.3.zip`: pacote local validado quanto aos arquivos obrigatórios, hashes internos e dry-run do CLI extraído.

## Auditoria de artefatos — 18/08/2026

| Verificação | Resultado | Interpretação correta |
| --- | --- | --- |
| Bundle portátil | Presente | Artefato local com DLLs modulares; não prova segurança ou prontidão pública. |
| ZIP portátil | Presente | Pacote local gerado; não substitui instalação/teste em máquina limpa. |
| SHA-256 do executável | `7B46C2027D1A6104602498F1654F38186B77009AF12FC02093D60FDCD1C81F27` | Confere com o manifesto interno da revisão `v0.1.0-dev.3` e com o arquivo incorporado no ZIP. |
| SHA-256 do ZIP | `753029D82EF089E0C47E91F629E4917C0C3A0150D78CA8C44685E7F9002204CF` | ZIP `v0.1.0-dev.3` desta rodada; contém 16 entradas somente sob `CLNXR-Portable/`, foi extraído, teve os hashes internos conferidos e o CLI concluiu dry-run redigido. |
| SBOM | `SPDX-2.3` válido | Inventário de desenvolvimento, ainda sem revisão de distribuição. |
| Assinatura Authenticode | `NotSigned` | Gate de beta público e 1.0 não atendido. |
| SDK .NET detectado | Não | O ambiente atual não permite recompilação independente por SDK; a build existente não prova reprodutibilidade. |
| Estado Git | Revisão publicada no `master`/`main` e tag `v0.1.0-dev.3` após a publicação desta rodada | O código e a documentação versionados permanecem separados dos binários de desenvolvimento na raiz (`CLNXR.exe` e `Program.cs`), que continuam ignorados fora do escopo público. |

## Decisão de término honesta

O desenvolvimento local atingiu um protótipo P0/P1 com validações específicas. O roadmap completo não pode ser declarado terminado porque as fases 6 e 7 exigem autoridade e estado externo que não podem ser simulados por código: identidade/marca, certificado de assinatura, ambiente limpo, publicação controlada, atualização, beta externo e revisão jurídica/segurança.

O próximo trabalho local legítimo é reduzir as lacunas de teste e de UI listadas acima. Para ultrapassar os gates externos, é necessária autorização e os respectivos recursos; não é aceitável substituir esses gates por um print, checksum ou compilação local.
