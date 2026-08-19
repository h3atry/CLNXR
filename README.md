# CLNXR (nome de trabalho)

Limpador de cache local para Windows, com análise antes de qualquer limpeza, seleção explícita, proteção de dados pessoais e recibo local verificável.

## Estado desta entrega

O bundle gerado em `artifacts\CLNXR-Portable\` é uma prévia de desenvolvimento local; o ZIP correspondente desta revisão está em `artifacts\CLNXR-Portable-0.1.0-dev.4.zip`. Ele foi compilado e o núcleo passou pelos testes de sandbox incluídos no projeto. Não foi validado visualmente nesta máquina, em máquina virtual limpa, com assinatura de código ou por beta público; portanto, não deve ser apresentado como release público ou como prova de compatibilidade universal.

O nome `CLNXR` é provisório. Marca, domínio, identificadores públicos e assinatura de código permanecem fora do escopo local até validação própria.

Repositório público e pré-release técnico: [github.com/h3atry/CLNXR](https://github.com/h3atry/CLNXR). O release é experimental e não substitui assinatura, VM limpa ou auditoria.

## Proteções atuais

- A análise é somente leitura.
- A limpeza exige revisão, seleção e confirmação.
- Cookies, logins, dados de sessão, histórico, Downloads, saves e arquivos pessoais não fazem parte das regras.
- Junctions, symlinks e demais reparse points são bloqueados durante análise e antes de remover itens.
- O aplicativo não mata processos nem se eleva automaticamente; arquivos em uso ou sem acesso são pulados.
- Um recibo JSON local com hash é salvo após cada limpeza, pode ser verificado e lido em visualizador estruturado somente leitura.

## Limites atuais

- O bundle portátil contém o executável gráfico, o `CLNXR.Cli.exe` e as DLLs modulares `Clnxr.*`; ele usa o .NET Framework do Windows e não é uma distribuição self-contained validada para máquinas sem esse runtime.
- Perfis P1 de Jogos (cache Unreal) e Desenvolvedor (NuGet, npm, pnpm, Yarn, pip, uv, Gradle, Maven e Cargo) existem como regras `REVIEW`/`ADVANCED`, desmarcadas por padrão. O perfil Completo inclui caches GPU Intel/AMD/NVIDIA, caches de navegador restritos a subpastas conhecidas e caches de apps fechados (Discord, Teams, Epic Launcher, Spotify e um caminho Electron genérico delimitado), sempre com proteção dos perfis e dados de sessão. A Lixeira é uma ferramenta separada que consulta primeiro e só chama a API oficial do Windows após confirmação. Para componentes do sistema, o CLNXR apenas abre o Storage Sense oficial após confirmação; ele não exclui componentes do Windows por conta própria.
- O catálogo Windows é carregado de `src/Clnxr.Platform.Windows/rules/windows.v1.json`, embutido no assembly e validado por schema, versão, IDs únicos, perfis e riscos antes da análise. O pacote ainda não é assinado nem atualizado por canal autenticado.
- Regras personalizadas podem ser criadas pela página Regras: o usuário escolhe uma pasta, idade mínima, extensões, exclusões e atribuição; o CLNXR mostra uma prévia redigida antes de salvar. Elas são persistidas localmente em `%LocalAppData%\\CLNXR\\Rules\\custom-rules.v1.json`, entram somente no perfil Personalizado, são sempre `ADVANCED` e permanecem `unsigned`; raízes protegidas, perfil pessoal inteiro, Downloads e reparse points são recusados.
- Mapa de disco, Arquivos grandes e Duplicados são ferramentas P1 de inventário: executam apenas depois de ação explícita, são canceláveis, ignoram reparse points e não oferecem exclusão nesta versão. Arquivos grandes mostram no máximo 100 resultados a partir de 512 MB; duplicados usam SHA-256 em candidatos a partir de 64 MB e limitam o hash a 10.000 arquivos.
- A página Ferramentas também oferece o Explorador de inicialização, o Inspetor de arquivos bloqueados, o inventário de resíduos e o agendamento Seguro. O explorador enumera Run/RunOnce e pastas de Inicialização; após confirmação, somente valores `HKCU` podem ser desabilitados com backup local reversível e restauração explícita. `HKLM`, pastas e comandos não são alterados. O inspetor consulta o Restart Manager sem encerrar processos. Resíduos são apenas candidatos baseados em `InstallLocation` declarado; nenhum desinstalador ou chave é executado/removido. O agendamento usa apenas o perfil Seguro, horário fixo e `schtasks.exe` sem elevação automática, e pode ser removido pelo próprio botão.
- Não há atualizador, assinatura de código, SBOM de release assinado, instalador ou telemetria.
- A persistência de regras personalizadas é local ao usuário e não acompanha automaticamente o ZIP portátil; importar/exportar regras e assinatura/marketplace continuam fora desta prévia.

## CLI local

`CLNXR.Cli.exe` executa uma análise em modo dry-run por padrão e produz JSON para stdout ou para `--json caminho`. A seleção padrão é somente `SAFE`; `REVIEW` exige `--allow-review`, `ADVANCED` exige `--allow-advanced` junto com `--allow-review`, e a execução real exige `--clean --yes`. IDs explícitos podem ser passados com `--rules id1,id2`. O CLI não aceita regras `BLOCKED`, não usa shell administrativo arbitrário e retorna códigos de saída documentados em `CLNXR.Cli.exe --help`.

## Estrutura

```text
src/Clnxr.Core/             domínio e planos imutáveis
src/Clnxr.Safety/           política de caminho, áreas protegidas e reparse points
src/Clnxr.Actions/          executor com revalidação e cancelamento
src/Clnxr.Evidence/         recibos locais e hash
src/Clnxr.Platform.Windows/ regras e descoberta de Windows
src/Clnxr.Application/      casos de uso que unem scan, plano, execução, recibos e ferramentas
src/Clnxr.Desktop/          adaptador WinForms provisório
src/Clnxr.Cli/              modo CLI dry-run/JSON com confirmação explícita
tests/Clnxr.Safety.Tests/   testes de sandbox
tests/Clnxr.WindowsScan.Smoke/ scan real somente leitura
tests/Clnxr.Desktop.Smoke/  construção não visual da janela WinForms
tests/Clnxr.Cli.Smoke/      ajuda, códigos de uso e dry-run JSON
docs/                       arquitetura, segurança, UI, regras e gates

`CHANGELOG.md` registra a prévia local e `THIRD_PARTY_NOTICES.md` descreve as dependências do bundle.
```

## Compilação usada nesta máquina

O ambiente atual não possui SDK moderno do .NET. O projeto possui [CLNXR.sln](CLNXR.sln) e doze projetos .NET Framework 4.0 separados: Core, Safety, Actions, Evidence, Platform.Windows, Application, Desktop, CLI e quatro executáveis de teste. O MSBuild clássico conseguiu compilar e executar esses assemblies em Release nesta máquina.

Há duas limitações reais desse build: faltam o targeting pack/referência oficial do .NET Framework 4.0 e as referências usadas pelo ambiente são AMD64 enquanto os projetos permanecem MSIL. O MSBuild emite ambos os avisos; por isso essa validação demonstra separação de módulos e carregamento local, não uma build reprodutível de release. O executável portátil de desenvolvimento continua gerado pelo compilador C# disponível em `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

Não instalar SDK ou targeting pack por conta própria é deliberado: isso altera o ambiente da máquina e precisa de autorização explícita.

## Próxima validação manual

O checklist reproduzível de UI/DPI está em [docs/CLNXR_MANUAL_UI_CHECKLIST.md](docs/CLNXR_MANUAL_UI_CHECKLIST.md). Ele não autoriza limpeza real: serve para observar o bundle portátil, especialmente as telas de histórico e ferramentas P1.
