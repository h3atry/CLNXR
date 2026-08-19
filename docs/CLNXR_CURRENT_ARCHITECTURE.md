# CLNXR — Arquitetura atual (auditoria de 18/08/2026)

## Escopo e evidência

Esta auditoria descreve o estado verificável em 18 de agosto de 2026 em `D:\Projects\limpador`. Build, testes e pacote são evidências locais; não equivalem a assinatura, compatibilidade universal, ausência de malware ou prontidão de beta.

- Estado atual: solução modular em `src/` com executável gráfico, CLI, pacote declarativo de regras Windows e quatro smokes separados.
- Controle de versão: repositório próprio com commits publicados em `master`/`main` e pré-release técnico no GitHub; os gates externos continuam pendentes.
- Ferramenta de compilação disponível: compilador C# do .NET Framework 4.x, limitado a C# 5. Não há SDK/targeting pack oficial moderno fixado.
- Execução do artefato: smokes carregam os assemblies, mas a UI não foi validada visualmente. A análise estrutural não comprova comportamento em todas as versões do Windows.

## Componentes atuais

| Componente do bundle portátil | Responsabilidade observada | Problema arquitetural |
| --- | --- | --- |
| `MainForm` | Janela, seleção, confirmação, scan e limpeza assíncrona | Ainda concentra orquestração de tela; a separação em serviços existe, a grade de resultados usa virtualização de linhas e a UI permanece WinForms provisória. |
| `WindowsCandidateScanner` | Descobre perfis/discos e cria candidatos conhecidos | Usa regras versionadas do pacote declarativo; assinatura e atualização do pacote ainda não existem. |
| `FileMeasurement` | Percorre diretórios e mede arquivos | Produz contagens e avisos estruturados, mas ainda não há benchmark de volume grande. |
| `CleanupExecutor` | Revalida política, processo, reparse point e remove somente alvos elegíveis | Não substitui testes de ACL/concorrência em VM e não encerra processos por design. |
| `ScanSession` / `CleanupReceipt` | Resultado de análise, ação, contagens, bytes, saltos e hash persistido | Migração formal de versões de recibo e assinatura do recibo ainda não existem. |
| `WindowsRulePack` | Carrega JSON embutido, valida schema, versão, IDs, perfis e riscos | O pacote é versionado, mas não assinado nem atualizável de forma autenticada. |
| `StartupExplorerService` | Enumera HKCU/HKLM Run/RunOnce e pastas; permite apenas mutação HKCU com revalidação e backup reversível | Não executa comandos, não toca HKLM/pastas e não eleva automaticamente; restauração real ainda requer teste manual controlado. |
| `UninstallResidualService` | Inventaria entradas conhecidas e marca somente `InstallLocation` declarado ausente | Não adivinha sobras, não remove Registro e não executa desinstaladores. |
| `ScheduledCleanupService` | Constrói e executa comando fixo de `schtasks.exe` para perfil Seguro diário; remove a mesma tarefa | Contrato testado sem mutação; execução real depende de Task Scheduler/conta e confirmação manual. |
| `NetworkUtilitiesService` | Catálogo fechado para diagnóstico, Flush DNS e planos manuais de reset | Não executa Winsock/TCP-IP automaticamente; sem shell arbitrário e sem elevação automática. |
| `SystemRepairService` | Catálogo fechado de SFC/DISM/CHKDSK somente verificação, com saída limitada | Não dispara switches de reparo; execução depende de utilitários, privilégios e estado do Windows. |

## Comportamento já presente

Fatos observados no código:

1. O aplicativo oferece análise antes da confirmação de limpeza.
2. Examina unidades fixas e removíveis prontas e procura perfis abaixo de `X:\Users`.
3. Limita os alvos a temporários, relatórios/dumps, caches de GPU e miniaturas conhecidas.
4. Não inclui explicitamente cookies, credenciais, histórico, Downloads, saves ou arquivos pessoais.
5. Ignora entradas marcadas como `ReparsePoint` durante a leitura e antes da exclusão recursiva.
6. A limpeza calcula bytes liberados somente para arquivos removidos com sucesso e executa uma nova análise após o fluxo de limpeza.
7. A página Ferramentas separa inventário de mutações: HKCU Run/RunOnce pode ser desabilitado com confirmação e desfazer; resíduos de desinstalação são somente candidatos; agendamento é opt-in, fixo e removível.
8. As ferramentas P3 usam comandos absolutos do diretório do sistema, sem shell; saídas são limitadas e podem ser persistidas somente no recibo local após confirmação.

## Limites e riscos atuais

| Severidade | Lacuna | Consequência |
| --- | --- | --- |
| Alta | O pacote declarativo ainda não tem assinatura nem mecanismo de atualização autenticada. | Uma distribuição pública não pode confiar em regras alteradas sem verificação criptográfica. |
| Alta | A UI/scan usa contratos de segurança e revalidação, mas faltam VM, ACL negada, hard link e concorrência ampla. | Fixtures locais reduzem risco conhecido, mas não cobrem todos os mecanismos do Windows. |
| Média | A grade de resultados agora usa `DataGridView.VirtualMode`, mas não há benchmark de 60 FPS/volumes grandes nem teste visual. | A virtualização reduz a criação de controles; memória, tempo de scan e responsividade ainda precisam de medição em volume real. |
| Média | Preferências locais de movimento reduzido, idioma/tema e opt-in de atualização são persistidas em INI sanitizado; idioma/tema continuam fixos no protótipo. | Não há tradução adicional, troca visual de tema ou updater; o arquivo local não é prova de canal de atualização seguro. |
| Média | Agendamento e mutação HKCU têm contratos fixos, mas execução real foi mantida fora dos testes automatizados. | O contrato é verificável; comportamento de conta/Task Scheduler/Registro precisa de validação manual. |
| Média | Há repositório e solução publicados, mas targeting pack/SDK fixado e pipeline reprodutível ainda não existem. | A build local é verificável, mas não é release reproduzível. |
| Baixa | O nome visual é provisório. | Não bloqueia o núcleo, mas bloqueia publicação até validação de marca. |

## Decisão de migração

O motor atual é modular e reutilizável, mas a separação de orquestração de tela ainda deve melhorar antes de uma troca grande de interface. A recomendação continua sendo manter o executável WinForms como protótipo até existir SDK moderno, validação visual e uma interface adaptadora nova.

O roadmap sugere PySide6/QML apenas para um motor Python existente. Este projeto é C#, então adotar Python e QML agora criaria uma reescrita dupla sem benefício demonstrado. A trilha local proposta é C# com núcleo independente da GUI; a escolha entre WinForms temporário, WPF ou WinUI será feita somente depois de haver SDK moderno e protótipo visual validado.

## Linha de base funcional a preservar

- Analisar antes de limpar.
- Permitir desmarcar achados.
- Manter navegador, sessão, cookies, histórico, Downloads, saves e arquivos pessoais fora do escopo.
- Percorrer todos os discos locais elegíveis sem seguir links/junctions.
- Informar remoções, bytes efetivamente liberados e itens ignorados.
