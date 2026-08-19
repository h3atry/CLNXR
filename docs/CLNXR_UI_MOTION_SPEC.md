# CLNXR — Especificação de interface e movimento

## Princípios de interface

1. A interface deve provar o que vai fazer antes da confirmação, e provar o que fez depois.
2. Segurança não pode ficar escondida em texto pequeno: cada resultado mostra regra, risco, escopo e motivo.
3. Ações destrutivas exigem seleção explícita e confirmação; o botão não deve prometer espaço liberado antes da medição posterior.
4. Navegadores, logins, cookies, histórico, Downloads, saves e arquivos pessoais ficam declarados como escopo protegido e ausentes da lista de limpeza.

## Navegação alvo

| Área | Conteúdo |
| --- | --- |
| Visão geral | Espaço recuperado comprovado, último recibo, atalhos para analisar e histórico. |
| Analisar | Perfil, unidades elegíveis, progresso, cancelamento e resumo de achados. |
| Resultados | Árvore/grade virtualizada, busca, filtros de risco/categoria/unidade e painel de detalhes. |
| Ferramentas | Lixeira, limpeza oficial do Windows e ferramentas P1 separadas por risco. |
| Histórico | Recibos locais, exportação e comparação entre sessões. |
| Regras | Catálogo somente leitura com versão, explicação e guardas; regras personalizadas são avançadas. |
| Configurações | Idioma, tema, movimento reduzido, privacidade e diretório de recibos. |

## Fluxo de limpeza

1. **Analisar:** mostra estado determinístico, unidades analisadas e progresso.
2. **Revisar:** lista estimativa, risco e explicação; nada é removido.
3. **Confirmar:** mostra contagem, risco, regras e aviso de que itens bloqueados serão ignorados.
4. **Executar:** apresenta categoria atual, itens concluídos, cancelamento e skips em tempo real.
5. **Concluído:** exibe arquivos removidos, bytes de arquivos efetivamente removidos, itens pulados e resultado da nova medição.
6. **Recibo:** permite abrir/copiar/exportar o manifesto local da sessão.

## Linguagem de resultado

- Use “estimativa” antes da limpeza.
- Use “bytes de arquivos removidos” durante a execução.
- Use “resultado verificado por nova análise” somente após revarrer os alvos processados.
- Não declare ganho de desempenho, correção de Windows ou bytes liberados no disco sem evidência correspondente.

## Tokens visuais

| Papel | Diretriz |
| --- | --- |
| Fundo | Grafite escuro, contraste suficiente para texto e estados. |
| Primário | Ciano para navegação, seleção e progresso. |
| Marca | Magenta apenas como acento opcional, não como cor predominante. |
| Êxito | Verde para ações finalizadas e verificadas. |
| Revisão | Âmbar para `REVIEW` e estados que exigem atenção. |
| Erro | Vermelho reservado para falha/risco real. |
| Tipografia | Interface legível, sem visual “gamer neon”. |

## Movimento e desempenho

- Meta: manter a janela responsiva; medição, hash, scan e limpeza não rodam no thread de UI.
- Todo trabalho longo recebe token de cancelamento e atualizações limitadas para não saturar a interface.
- O modo de movimento reduzido remove transições não essenciais e é inicializado pela preferência de animação do Windows; a escolha pode ser salva localmente.
- A página de resultados usa `DataGridView.VirtualMode` para virtualizar linhas; não carrega objetos visuais por arquivo encontrado.
- Animações de estado devem permanecer curtas e nunca atrasar a confirmação de uma ação.
