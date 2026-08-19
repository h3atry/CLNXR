# CLNXR — Registro de validação local P0

Data: 18 de agosto de 2026. Escopo: `D:\Projects\limpador`.

## Evidência concluída

| Bloco | Resultado | O que foi comprovado |
| --- | --- | --- |
| Compilação do núcleo e testes | Passou | O compilador C# do .NET Framework aceitou os módulos Core, Safety, Actions, Evidence, Platform Windows e Application. |
| Solução com assemblies físicos | Passou com avisos de ambiente | `CLNXR.sln` compilou Core, Safety, Actions, Evidence, Platform Windows, Application, Desktop, CLI e quatro executáveis de teste; os binários separados de teste foram executados. Faltam targeting pack oficial e alinhamento de arquitetura. |
| Sandbox `Clnxr.Safety.Tests` | Passou, 15 grupos | Política de caminho, limpeza de fixture pelo serviço de aplicação, recibo/hash/redação de caminho, cancelamento/processo, arquivo real bloqueado, catálogo P0/P1, pacote declarativo versionado e perfil Personalizado, caches de navegador protegidos, filtro de idade, regras personalizadas com prévia/limpeza explícita e persistência local, ferramentas P1/P2 somente leitura, inventário de resíduos, contrato de agendamento, lista/recusa de mutação reversível de inicialização, cancelamento no meio de árvore, junction/TOCTOU real e symlink de diretório real em sandbox. |
| Smoke `Clnxr.WindowsScan.Smoke` | Passou, somente leitura | O scanner Seguro completou neste Windows sem abrir a UI nem remover arquivos. |
| Smoke `Clnxr.Desktop.Smoke` | Passou, não visual | A janela desktop foi construída e descartada em STA sem ser exibida e sem iniciar análise ou limpeza. |
| Consulta da Lixeira | Passou, somente leitura | A API oficial do Windows respondeu à consulta global sem esvaziar a Lixeira; a referência observada foi 0 itens e 0 bytes. |
| Build desktop/CLI | Passou com avisos de ambiente | O build físico gerou `src\Clnxr.Desktop\bin\Release\CLNXR-Portable.exe` e `src\Clnxr.Cli\bin\Release\CLNXR.Cli.exe`; o executável gráfico da revisão `v0.1.0-dev.4` tem SHA-256 `66EE7F3ABD01B307CE2C2BA4DAC5800A23A2381D77441694716468DC4138FDE3`. O processo ainda usa referências do GAC por ausência do targeting pack oficial e tem alerta de arquitetura MSIL/AMD64. |
| Pacote portátil local | Gerado e conferido | ZIP `v0.1.0-dev.4` contém 16 entradas somente sob `CLNXR-Portable/`, 15 hashes internos sem divergência, e o CLI extraído concluiu dry-run JSON sem expor o nome do usuário. SHA-256 do ZIP: `5512707B707AC672B8505ED3809D828B642F2E19D420389A292357BE5B0C6F08`. |

## Observação do smoke test real

Na execução de referência, a análise Segura encontrou um achado elegível sob o temporário do perfil e detectou reparse points nesse mesmo escopo. Esses reparse points foram classificados como preservados e não se tornaram achados. Contagens de arquivos/tamanho em `%LocalAppData%\Temp` variam entre execuções porque esse diretório é volátil; não devem ser usados como benchmark de desempenho.

## O que esta evidência não prova

- comportamento visual da UI em interação humana;
- esvaziamento real da Lixeira (não foi solicitado nem executado);
- limpeza real em cache de aplicativos de terceiros;
- segurança contra todos os tipos de link, ACL ou concorrência/TOCTOU; há cobertura local de junction/TOCTOU e de arquivo real bloqueado, mas não de todos os mecanismos do Windows;
- compatibilidade em outra versão de Windows ou em máquina limpa;
- assinatura, reputação SmartScreen, ausência de malware ou adequação a beta público.
- build reproduzível em ambiente limpo/SDK fixado; o MSBuild local usou assemblies do GAC por falta do targeting pack oficial.

As lacunas permanecem listadas nos gates de release e não podem ser convertidas em “concluído” sem suas próprias evidências.
