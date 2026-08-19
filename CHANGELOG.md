# Changelog

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
