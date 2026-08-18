# CLNXR — Checklist manual de UI e DPI

Este roteiro é uma validação manual do build portátil. Não execute limpeza real para cumprir esta etapa; a evidência de UI não substitui as barreiras de segurança nem os testes de sandbox.

## Pré-condições

- Extrair `artifacts\CLNXR-Portable-0.1.0-dev.zip`, abrir `CLNXR-Portable\CLNXR-Portable.exe` e conferir os hashes em `CLNXR-Portable\SHA256SUMS.txt`. O mesmo diretório contém `CLNXR.Cli.exe` para dry-run/JSON.
- Registrar versão do Windows, resolução, escala do sistema e se o usuário tem alto contraste/movimento reduzido ativo.
- Não estar em sessão de jogo ou outra atividade que não possa ser interrompida.
- Não selecionar nem confirmar remoção de achados reais nesta validação.

## Casos obrigatórios

| Caso | Ação manual | Aceite observável |
| --- | --- | --- |
| Inicialização | Abrir o executável e ficar na Visão geral. | A janela aparece sem análise automática, sem pedido de elevação e sem alteração de arquivos. |
| Escala | Reabrir em 100%, 125% e 150% de escala do Windows. | Texto, botões, grade e rodapé permanecem legíveis; nenhum controle essencial fica inacessível. |
| Navegação | Abrir Analisar, Resultados, Ferramentas, Histórico, Regras e Configurações, sem acionar limpeza. | Título, descrição e controles correspondem à seção; não há travamento nem ação implícita. |
| Ferramentas P1 | Conferir Mapa de disco, Arquivos grandes e Duplicados. Não iniciar varredura se houver dados sensíveis na tela. | Os textos deixam claro que são inventário somente leitura; Duplicados pede confirmação antes de calcular hash. |
| Histórico vazio | Abrir Histórico sem selecionar arquivo externo. | A página informa que não há recibos, não envia dados e deixa ações dependentes de seleção desativadas. |
| Histórico com recibo | Após uma futura limpeza controlada em fixture/VM, abrir Histórico, selecionar um recibo e usar Ver recibo. | A tabela mostra campos e resultados; o estado de integridade SHA-256 fica visível; não há edição do JSON. |
| Recibo inválido | Em uma fixture, duplicar um recibo e alterar uma letra sem atualizar o hash. | Histórico indica falha de integridade; o visualizador ainda pode informar o conteúdo local, mas não declara o recibo íntegro. |
| Cancelamento visual | Somente em fixture/VM, iniciar análise e cancelar. | O botão Cancelar responde, o estado final informa cancelamento e nenhuma limpeza é iniciada pela análise. |

## Resultado a registrar

Para cada caso, anotar `passou`, `falhou` ou `não executado`, junto de versão do artefato, escala, resolução, captura de tela opcional e defeito reproduzível. “Abriu uma vez” não prova DPI, acessibilidade, desempenho ou compatibilidade geral.

## Lacunas que este checklist não fecha

- não valida esvaziamento da Lixeira em VM;
- não prova ACL negada, antivírus, SmartScreen, assinatura ou máquina limpa;
- não mede desempenho em volumes grandes;
- não substitui revisão de acessibilidade, contraste, teclado e leitor de tela;
- não autoriza beta público ou 1.0.
