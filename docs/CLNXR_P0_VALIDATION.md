# CLNXR — Registro de validação local P0

Data: 19 de agosto de 2026. Escopo: `D:\Projects\limpador`.

## Evidência concluída

| Bloco | Resultado | O que foi comprovado |
| --- | --- | --- |
| Compilação do núcleo e testes | Passou | O compilador C# do .NET Framework aceitou os módulos Core, Safety, Actions, Evidence, Platform.Windows e Application. |
| Solução com assemblies físicos | Passou com avisos de ambiente | `CLNXR.sln` compilou Core, Safety, Actions, Evidence, Platform.Windows, Application, Desktop, CLI e quatro executáveis de teste; os binários separados de teste foram executados. Há dependência do runtime .NET Framework do sistema e ausência de targeting pack moderno no ambiente. |
| Sandbox `Clnxr.Safety.Tests` | Passou, 21 grupos | Política de caminho, limpeza de fixture pelo serviço de aplicação, recibo/hash/redação de caminho, cancelamento/processo, arquivo real bloqueado, catálogo P0/P1, pacote declarativo versionado e perfil Personalizado, caches de navegador protegidos, filtro de idade, regras personalizadas com prévia/limpeza explícita e persistência local, ferramentas P1/P2 somente leitura, inventário de resíduos, contrato de agendamento, lista/recusa de mutação reversível de inicialização, cancelamento no meio de árvore, junction/TOCTOU real, symlink de diretório real e negação de acesso em cleanup local em sandbox. |
| Smoke `Clnxr.WindowsScan.Smoke` | Passou, somente leitura | O scanner Seguro completou neste Windows sem abrir a UI nem remover arquivos. |
| Smoke `Clnxr.Desktop.Smoke` | Passou, não visual | A janela desktop foi construída e descartada em STA sem ser exibida e sem iniciar análise ou limpeza. |
| Consulta da Lixeira | Passou, somente leitura | A API oficial do Windows respondeu à consulta global sem esvaziar a Lixeira; a referência observada foi 0 itens e 0 bytes. |
| Build desktop/CLI | Passou | O build físico gerou `src\Clnxr.Desktop\bin\Release\CLNXR-Portable.exe`, `src\Clnxr.Cli\bin\Release\CLNXR.Cli.exe` e `artifacts\CLNXR-Portable-SingleFile\CLNXR.exe`; o executável único final foi atualizado para SHA-256 `31AB96513CD0DD966FA8DAB4FC71DFC462EEB3E0ADC95C8044AC162524723BF3` e não depende de DLLs `Clnxr.*` no diretório final. |
| Artefato portátil local | Gerado e conferido | A entrega de distribuição deste ciclo é `artifacts\CLNXR-Portable-SingleFile\CLNXR.exe`; não há pacote ZIP de release em uso neste ciclo. |
| Teste de desempenho P1 (carga sintética) | Parcial | Script `tmp\Clnxr.StorageAnalysisPerfSmoke.cs` executou varredura de P1 em carga sintética controlada (`3.000` arquivos por bucket, total de 9.060 arquivos) com resultados: `DiskMapElapsedMs=3751`, `LargeElapsedMs=3567`, `DuplicatesElapsedMs=3317`, 1 grupo de duplicados (`46.399.488` bytes recuperáveis) e 1 issue residual em cada etapa de análise (esperado no cenário de benchmark). Isso não substitui validação visual e real-world. |

## Observação do smoke test real

Na execução de referência, a análise Segura encontrou um achado elegível sob o temporário do perfil e detectou reparse points nesse mesmo escopo. Esses reparse points foram classificados como preservados e não se tornaram achados. Contagens de arquivos/tamanho em `%LocalAppData%\Temp` variam entre execuções porque esse diretório é volátil; não devem ser usadas como benchmark de desempenho.

## O que esta evidência não prova

- comportamento visual da UI em interação humana;
- esvaziamento real da Lixeira (não foi solicitado nem executado);
- limpeza real em cache de aplicativos de terceiros;
- segurança contra todos os tipos de link, ACL e concorrência/TOCTOU; há cobertura local de junction/TOCTOU, de arquivo real bloqueado e de negação de acesso (com fallback controlado para atributo de somente-leitura), mas ainda não cobre todos os mecanismos do Windows;
- compatibilidade em outra versão de Windows ou em máquina limpa;
- assinatura, reputação SmartScreen, ausência de malware ou adequação a beta público.
- build reproduzível em ambiente limpo/SDK fixado; o ambiente atual não possui SDK moderno do .NET.

As lacunas permanecem listadas nos gates de release e não podem ser convertidas em “concluído” sem suas próprias evidências.
