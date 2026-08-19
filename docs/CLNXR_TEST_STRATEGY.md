# CLNXR — Estratégia de teste e evidência

## Testes já executados

`tests/Clnxr.Safety.Tests` foi compilado e executado em sandbox temporário nesta máquina em 18 de agosto de 2026.

| Grupo | Evidência | Limite |
| --- | --- | --- |
| Política de caminho | bloqueia `Downloads`, alvo fora da raiz e regra `BLOCKED`. | Não simula todas as ACLs ou links do sistema real. |
| Limpeza de fixture | remove dois arquivos, registra 10 bytes e grava recibo verificável; também grava recibo verificável de ferramenta. | Não mede ganho real de espaço do volume. |
| Integridade do recibo salvo | reabre o JSON, exige `SchemaVersion`, valida o hash registrado e rejeita uma cópia adulterada da fixture. | Hash local detecta alteração acidental ou não autorizada, mas não substitui assinatura digital ou migração futura. |
| Cancelamento/processo | preserva arquivos quando cancelado ou com processo relacionado em uso. | O inspetor de processo foi simulado no teste. |
| Arquivo bloqueado | abre um arquivo real da fixture sem compartilhar exclusão; a limpeza o preserva e o recibo conta o item pulado. | Não representa todos os locks de aplicativos de terceiros ou permissões de ACL. |
| Catálogo de perfis e regras P0/P1 | confirma Seguro/Completo/Jogos/Desenvolvedor/Personalizado, caches GPU Intel/AMD/NVIDIA, caches de navegador isolados, caches de apps fechados e stores de ferramentas de desenvolvimento. | Não prova que cada cache real de terceiros é seguro para apagar. |
| Cache de navegador e privacidade | cria caminhos simulados de Chrome e Firefox; permite apenas `Cache`/`cache2`, bloqueia `Login Data` e `logins.json`, expande perfis com curinga e exige processo fechado no catálogo. | Ainda não é validação em perfis reais ou em máquina virtual limpa. |
| Filtro de idade e recibo redigido | mede e remove somente arquivos com sete dias ou mais; preserva arquivo recente e não persiste o nome do usuário no caminho do recibo. | A redaction atual não substitui uma revisão formal de PII para todas as mensagens futuras. |
| Junction e TOCTOU | cria junction NTFS real em sandbox, bloqueia a junction e bloqueia o alvo que foi trocado por junction depois da criação do plano. | Ainda não cobre todas as combinações de ACL, hard link e concorrência de outro processo. |
| Symlink de diretório | cria symlink NTFS real em sandbox, verifica o reparse point e bloqueia-o como alvo antes da limpeza. | Não cobre todos os tipos de hard link, ACL ou mount point. |
| Shell desktop | constrói e descarta a janela desktop em STA sem exibi-la, sem análise e sem limpeza. | Não substitui revisão visual, DPI, acessibilidade ou interação humana. |
| Cancelamento P1 no meio da árvore | cria 128 arquivos de fixture, cancela no callback do 64º arquivo e verifica preservação dos extremos. | Não mede desempenho real em volume grande nem concorrência de processo externo. |
| Ferramentas P1 somente leitura | mede uma fixture, encontra arquivo grande, agrupa conteúdo duplicado por SHA-256, ignora junction e preserva todos os arquivos. | Não substitui teste de desempenho em discos grandes ou dados reais. |
| Ferramentas P2 e mutações reversíveis | enumera inicialização, inventaria resíduos, consulta arquivo pelo Restart Manager, valida plano de `schtasks.exe` e recusa mutação de entrada não suportada; o serviço de inicialização só permite `HKCU` e a UI exige confirmação. | Não substitui teste visual, ACL, VM limpa, locks de aplicativos de terceiros, execução real de `schtasks.exe` ou restauração de um valor real do usuário. |
| Contratos P3 de rede e reparo | valida catálogo fechado de diagnóstico/Flush DNS/resets manuais, argumentos fixos, recusa de ações arbitrárias, volume `X:` validado e ausência de `/f`/switches destrutivos; reset Winsock é recusado como execução silenciosa. | Não executa reset de rede nem reparo do sistema no sandbox; resultado real depende de privilégios, versão do Windows, tempo e estado da máquina. |
| Pacote declarativo de regras | carrega o recurso `clnxr.rules.windows.v1`, verifica versão do catálogo, IDs únicos, perfis e riscos antes de expor regras ao scanner. | Não prova assinatura digital, atualização segura ou revisão externa do pacote. |
| Regras personalizadas | cria uma regra com Unicode, idade mínima, extensão e exclusão; exige prévia real, mantém risco `ADVANCED`, redige exemplos, limpa somente itens explícitos e testa persistência versionada local (`save/list/delete`). | Não prova assinatura, importação/marketplace, portabilidade entre usuários ou segurança de uma raiz real escolhida fora da fixture. |
| CLI smoke | executa `--help`, rejeita perfil Personalizado sem IDs e valida dry-run JSON sem alterar arquivos. | Não prova limpeza destrutiva, compatibilidade de shell ou distribuição pública. |

## Matriz obrigatória antes de beta

| Camada | Testes necessários |
| --- | --- |
| Core | transições de sessão, plano vazio/bloqueado, totais e serialização. |
| Safety | normalização, escape de raiz, reparse point/junction, dados protegidos e TOCTOU. |
| Rules | fixture normal, ausente, negada, arquivo bloqueado e upgrade de versão por regra. |
| Actions | cancelamento durante recursão, arquivo somente leitura, diretório em uso e resultado por alvo. |
| Evidence | hash, gravação atômica, histórico, corrupção e compatibilidade de versão. |
| UI | cancelamento, seleção de REVIEW, acessibilidade, escala de DPI e modo reduzido. |
| Integração | máquina virtual limpa, usuário padrão/admin, vários discos e cache real de apps fechados. |
| Release | hash de artefato, assinatura, SBOM, antivírus, atualização e rollback. |

## Regras de interpretação

Compilar prova apenas que o compilador aceitou o código. Um teste de fixture prova somente a fixture. A aprovação de beta exige o conjunto de testes acima e evidência reproduzível, não uma soma de prints ou mensagens de sucesso.
