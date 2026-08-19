# Changelog

## 0.1.0-dev.8 — 2026-08-19

- Bloqueia arquivos com mais de um hard link físico antes da remoção e preserva esses itens no serviço de dados próprios, reduzindo o risco de apagar um nome externo por meio de um candidato interno.
- Adiciona verificação local de envelopes de regras RSA/SHA-256 (`clnxr.rules.signed.v1`), com validação de schema e catálogo somente depois da assinatura; o contrato não baixa pacotes, não escolhe chaves confiáveis e não atualiza o aplicativo.
- O sandbox passa a cobrir adulteração de payload, chave incorreta, hard link externo e preservação dos dois nomes físicos; são 20 grupos executados.
- Mantém a entrega como pré-release técnico: chave pública de produção, canal autenticado, updater, assinatura Authenticode, VM limpa, validação visual e beta externo continuam pendentes.

## 0.1.0-dev.7 — 2026-08-19

- Adiciona remoção explícita dos dados próprios do CLNXR em `%LocalAppData%\\CLNXR`, limitada a recibos, regras e configurações do aplicativo; a raiz é preservada para reinstalação e nenhum caminho arbitrário é aceito.
- A interface de Configurações mostra a prévia de arquivos/bytes, pede confirmação e informa removidos, preservados e avisos; cancelamento e reparse points preservam os dados.
- O sandbox passa a cobrir medição, remoção delimitada, proteção de arquivo externo, raiz preservada, rejeição de raiz arbitrária e cancelamento.
- Mantém a entrega como pré-release técnico; validação visual, benchmark de 60 FPS, ACL, VM limpa, assinatura, SmartScreen e beta externo continuam pendentes.

## 0.1.0-dev.6 — 2026-08-19

- Resultados passam a usar `DataGridView.VirtualMode`, mantendo filtros e seleção explícita sem criar um widget por achado.
- Configurações passam a persistir apenas preferências locais de movimento reduzido, idioma/tema fixos e opt-in de atualização; nenhum segredo, caminho pessoal ou telemetria é salvo.
- O modo de movimento reduzido é inicializado a partir da preferência do Windows e pode ser salvo/retornado ao padrão pela interface.
- Smoke da janela verifica o contrato de grade virtualizada e o sandbox passa a cobrir persistência e sanitização de preferências.
- Mantém a entrega como pré-release técnico; faltam validação visual, benchmark de 60 FPS, SDK/targeting pack fixado, assinatura, VM limpa e beta externo.

## 0.1.0-dev.5 — 2026-08-18

- Adiciona `NetworkUtilitiesService`: diagnóstico local com `ipconfig /all`, Flush DNS confirmável e planos manuais explícitos para Winsock/TCP-IP, sem shell arbitrário ou elevação automática.
- Adiciona `SystemRepairService`: verificações confirmáveis e não destrutivas para SFC, DISM e CHKDSK; switches de reparo destrutivo não entram no catálogo.
- Integra as ferramentas P3 à página Ferramentas, grava saída limitada em recibos locais e amplia os testes de segurança para 16 grupos.
- Mantém a entrega como pré-release técnico; não foram fechados assinatura, VM limpa, teste visual, beta externo ou compatibilidade universal.

## 0.1.0-dev.4 — 2026-08-18

- Adiciona inventário de resíduos de desinstalação baseado somente em `InstallLocation` declarado; não remove Registro nem executa desinstaladores.
- Adiciona desabilitação reversível de entradas `HKCU` Run/RunOnce, com confirmação, revalidação contra TOCTOU e backup local para restaurar; `HKLM`, pastas e comandos não são alterados.
- Adiciona agendamento opt-in do perfil Seguro às 03:00 via `schtasks.exe`, com argumentos fixos, sem elevação automática, recibo local e remoção explícita.
- Integra essas ações à página Ferramentas e amplia o sandbox para 15 grupos; todos os quatro smokes foram executados após o rebuild Release.

## 0.1.0-dev.3 — 2026-08-18

- Adiciona Explorador de inicialização somente leitura para Run/RunOnce e pastas de Inicialização.
- Adiciona Inspetor de arquivos bloqueados somente leitura usando o Restart Manager, sem encerrar ou reiniciar processos.
- Integra as duas ferramentas à página Ferramentas e amplia os testes de segurança para 13 grupos.

## 0.1.0-dev.2 — 2026-08-18

- Regras personalizadas locais com editor na página Regras, prévia redigida antes de salvar, pasta/idade/extensões/exclusões/atribuição e persistência versionada.
- Regras personalizadas sempre `ADVANCED`/`unsigned`, somente no perfil Personalizado, com revalidação e limpeza por itens explicitamente enumerados.
- Teste de sandbox ampliado para 12 grupos, incluindo persistência `save/list/delete` e limpeza de arquivos Unicode/restritos.
- Integração da seleção de regras personalizadas no scanner e na nova análise após a limpeza.

## 0.1.0-dev — 2026-08-18

- Catálogo Windows declarativo embutido e validado por schema, versão e IDs únicos.
- Perfis Seguro, Completo, Jogos, Desenvolvedor e Personalizado preservados.
- Caches delimitados de Discord, Teams, Epic Games Launcher, Spotify e Electron adicionados ao perfil Completo como `REVIEW`.
- Resultados com busca, filtro por risco, filtro de selecionados e detalhe por item.
- CLI com dry-run padrão, seleção explícita, códigos de saída e relatório JSON redigido.
- Ferramentas somente leitura para mapa de disco, arquivos grandes e duplicados.
- Testes de sandbox, scan real somente leitura, construção desktop e CLI smoke executados.

Esta linha continua sendo uma prévia de desenvolvimento. Não possui assinatura de código, atualizador, teste em VM limpa ou garantia de compatibilidade universal; o repositório e releases técnicos públicos não substituem esses gates.
