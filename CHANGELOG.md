# Changelog

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
