# CLNXR

Limpador local de arquivos temporários e caches do Windows com análise prévia, confirmação explícita e recibo verificável.

## Entrega atual

- O build principal alvo é: `artifacts\CLNXR-Portable-SingleFile\CLNXR.exe` (executável único, portável por pasta).
- O artefato acima é gerado com compilação direta de produção funcional e **sem DLLs `Clnxr.*` no mesmo diretório**, para distribuição portável por pasta.
- O projeto não tem release público ativo nesta etapa. A publicação no GitHub foi temporariamente suspensa até concluir o requisito de entrega local desejado e validar maturidade externa.

## Proteções ativas

- Análise antes de qualquer remoção.
- Prévia detalhada, seleção manual e confirmação para execução.
- Navegação de limpeza sem tocar navegador, logins, cookies, histórico, downloads, saves ou arquivos pessoais.
- Bloqueio de `ReparsePoint` e `hard links` físicos externos durante análise e execução.
- Sem encerramento automático de processos e sem elevação automática.
- Recibos locais JSON com hash de integridade por sessão.
- Limpeza dos dados próprios do CLNXR separada da limpeza de cache geral e com raiz dedicada preservada.

## O que o app cobre hoje

- Perfis: Seguro, Completo, Jogos, Desenvolvedor e Personalizado.
- Regras customizadas com prévia, validação e persistência local em `%LocalAppData%\CLNXR\Rules\custom-rules.v1.json`.
- Cache de disco e ferramentas de inspeção: mapa de armazenamento, arquivos grandes e duplicados (somente leitura, canceláveis, com proteção de segurança).
- Lixeira: consulta global e confirmação antes de qualquer ação.
- Inicialização e resíduos: exploração e desativação de entradas `HKCU` com backup reversível.
- Agendamento seguro: tarefa diária fixa e removível.
- Diagnóstico de rede (`ipconfig` somente leitura) e utilidades de verificação de sistema com confirmação manual.
- Interface de histórico e exportação de recibos JSON.

## Limites atuais (reais)

- Não há assinatura Authenticode, atualizador, telemetria ou distribuição autenticada.
- Não há validação em VM limpa, ACLs negativas, testes de desempenho em volume extremo ou validação de beta externo neste ciclo.
- O `targeting pack`/SDK moderno e o pipeline de build reproduzível ainda não fazem parte desta fase.
- O nome público, domínio e política final de publicação ainda dependem de validação externa.

## Estrutura principal

- `src/Clnxr.Core`: domínio e contratos
- `src/Clnxr.Safety`: regras e validações de segurança de caminho
- `src/Clnxr.Actions`: executor com revalidação e cancelamento
- `src/Clnxr.Evidence`: recibos locais
- `src/Clnxr.Platform.Windows`: regras de descoberta e ferramentas Windows
- `src/Clnxr.Application`: orquestração de casos de uso
- `src/Clnxr.Desktop`: UI WinForms (adaptador local atual)
- `src/Clnxr.Cli`: CLI com `--help`, dry-run e JSON
- `tests/*`: suítes de segurança/integração local
- `docs/*`: auditorias, roadmap e evidências

## Comando de build local

Observação: o projeto usa o compilador C# do .NET Framework local para gerar esse artefato único; nenhum arquivo auxiliar (DLL) de módulo é necessário no diretório final.

## Estado do CLI

`CLNXR.Cli.exe` e o executável gráfico preservam o modo seguro por padrão:

- scan não destrutivo por padrão (dry-run),
- `--yes` necessário para exclusão,
- regras `BLOCKED` jamais entram em limpeza,
- saída JSON disponível para integração.

## Validação e rastreabilidade

Consulte:

- `docs/CLNXR_ROADMAP_AUDIT.md`
- `docs/CLNXR_RELEASE_GATES.md`
- `docs/CLNXR_P0_VALIDATION.md`
- `CHANGELOG.md`
- `THIRD_PARTY_NOTICES.md`

## Próxima fase operacional

Concluir validações de VM limpa, testes de desempenho, assinatura e distribuição controlada são o limite objetivo para tornar a entrega adequada a testes beta.
