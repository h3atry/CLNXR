# CLNXR — Arquitetura-alvo

## Objetivo

Transformar o protótipo em um limpador portátil, local-first e auditável. O produto só pode propor ações que sejam explicáveis por regra, revalidadas no momento da execução e registradas em recibo local.

## Fronteiras de módulos

```text
CLNXR.Desktop (UI adaptadora)
        │
        ▼
CLNXR.Application (casos de uso, sessão e persistência de recibos)
 ├── CLNXR.Core (ScanSession, Finding, Rule, Risk, ActionResult, CleanupReceipt)
 ├── CLNXR.Safety (normalização, políticas, guardas, processo, cancelamento)
 ├── CLNXR.Actions (planejamento e execução de ações aprovadas)
 ├── CLNXR.Evidence (manifesto, hash, recibos, histórico local)
 └── CLNXR.Platform.Windows (discos, perfis, APIs oficiais e processos)
```

Dependências permitidas:

- `Core` não depende de Windows Forms, WPF, WinUI, disco, registro ou rede.
- `Safety`, `Actions`, `Evidence` e `Platform.Windows` dependem de `Core`, nunca da UI.
- `Desktop` depende de `Application` e de contratos em `Core`; não chama scanner, executor, API da Lixeira, launcher de Storage Sense ou persistência de recibos diretamente.
- Regras são dados declarativos versionados e validados; a UI apenas os apresenta e não inventa caminhos.

Estado de compilação atual: `CLNXR.sln` materializa os módulos como projetos .NET Framework separados. A solução e os quatro executáveis de teste foram compilados pelo MSBuild clássico e executados localmente, mas o ambiente ainda emite avisos por falta do targeting pack oficial e por referência AMD64 em projetos MSIL. Isso não satisfaz o gate de build reprodutível de release.

## Modelo de domínio inicial

| Tipo | Responsabilidade mínima |
| --- | --- |
| `ScanSession` | ID, perfil, início/fim, estado, cancelamento, versão do catálogo e resumo. |
| `Finding` | Regra, caminho candidato, tamanho estimado, risco, elegibilidade, guardas e motivo de bloqueio. |
| `Rule` | `rule_id`, versão, categoria, risco, escopos, guardas, explicação, ação e testes exigidos; a fonte operacional Windows é o pacote `clnxr.rules.windows.v1`. |
| `Risk` | `SAFE`, `REVIEW`, `ADVANCED`, `BLOCKED`. |
| `ActionPlan` | Conjunto imutável de achados selecionados após revisão e antes da execução. |
| `ActionResult` | Resultado por alvo: removido, pulado, bloqueado, falhou ou cancelado, com bytes e motivo. |
| `CleanupReceipt` | Manifesto final da sessão, totais antes/depois, regras, resultados, versão e integridade. |

## Fluxo obrigatório

```text
Escolher perfil
  → iniciar ScanSession
  → descobrir Finding por Rule
  → aplicar SafetyPolicy e medir
  → revisar / filtrar / selecionar
  → criar ActionPlan imutável
  → revalidar cada alvo no instante da ação
  → executar com cancelamento e eventos de progresso
  → nova medição limitada aos alvos processados
  → gravar CleanupReceipt local
```

Nenhum botão da UI pode ignorar o planejamento, a política ou o recibo.

## Perfis planejados

| Perfil | Escopo inicial | Estado |
| --- | --- | --- |
| Seguro | temporários conhecidos, relatórios de falha, dumps de apps e dados equivalentes com regra `SAFE` | P0 |
| Completo | Seguro + shader/GPU cache conhecido, miniaturas e caches de aplicativos fechados | P0, com guardas adicionais |
| Jogos | caches de shader e crash reports de launchers/jogos fechados | P1 |
| Desenvolvedor | caches de pacotes e ferramentas selecionadas pelo usuário | P1 |
| Personalizado | somente IDs de regras catalogadas escolhidos explicitamente pelo usuário; regras `BLOCKED` são excluídas | implementado na UI e no scanner |
| Avançado | somente regras explicitamente revisadas e nunca selecionadas por padrão | pós-P0 |

## Decisões ainda dependentes de ambiente externo

- Uma UI moderna C# exige instalar e fixar um SDK suportado; esse ambiente hoje não o possui.
- A UI final será WPF ou WinUI, não decidida por marketing. A decisão depende de uma prova de build, acessibilidade, portabilidade e manutenção.
- O nome público não será fixado até validação jurídica, domínio e identificadores sociais. `CLNXR` é a denominação de desenvolvimento adotada no projeto até validação dessa camada.
