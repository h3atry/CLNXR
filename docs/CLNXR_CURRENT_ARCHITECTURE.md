# CLNXR — Arquitetura atual (auditoria de linha de base)

## Escopo e evidência

Esta auditoria descreve o estado encontrado em 18 de agosto de 2026 em `D:\Projects\limpador`.

- Linha de base histórica: um único arquivo `Program.cs` (WinForms / .NET Framework) e o artefato anterior `H3ATRY Cache Cleaner.exe`.
- Estado atual: solução modular em `src/` com executável gráfico, CLI, pacote declarativo de regras Windows e quatro smokes separados.
- Controle de versão: na auditoria inicial não havia repositório próprio. O projeto agora possui `.git` próprio em `D:\Projects\limpador`, mas ainda não existe baseline commitado para release.
- Ferramenta de compilação disponível: compilador C# do .NET Framework 4.x, limitado a C# 5. Não há SDK moderno do .NET instalado.
- Execução do artefato: não foi usada como prova nesta auditoria. A análise é estrutural; ela não comprova comportamento em todas as versões do Windows.

## Componentes atuais

| Componente do bundle portátil | Responsabilidade observada | Problema arquitetural |
| --- | --- | --- |
| `MainForm` | Janela, seleção, confirmação, scan e limpeza assíncrona | Ainda concentra orquestração de tela; a separação em serviços existe, mas a UI permanece WinForms provisória. |
| `WindowsCandidateScanner` | Descobre perfis/discos e cria candidatos conhecidos | Usa regras versionadas do pacote declarativo; assinatura e atualização do pacote ainda não existem. |
| `FileMeasurement` | Percorre diretórios e mede arquivos | Produz contagens e avisos estruturados, mas ainda não há benchmark de volume grande. |
| `CleanupExecutor` | Revalida política, processo, reparse point e remove somente alvos elegíveis | Não substitui testes de ACL/concorrência em VM e não encerra processos por design. |
| `ScanSession` / `CleanupReceipt` | Resultado de análise, ação, contagens, bytes, saltos e hash persistido | Migração formal de versões de recibo e assinatura do recibo ainda não existem. |
| `WindowsRulePack` | Carrega JSON embutido, valida schema, versão, IDs, perfis e riscos | O pacote é versionado, mas não assinado nem atualizável de forma autenticada. |

## Comportamento já presente

Fatos observados no código:

1. O aplicativo oferece análise antes da confirmação de limpeza.
2. Examina unidades fixas e removíveis prontas e procura perfis abaixo de `X:\Users`.
3. Limita os alvos a temporários, relatórios/dumps, caches de GPU e miniaturas conhecidas.
4. Não inclui explicitamente cookies, credenciais, histórico, Downloads, saves ou arquivos pessoais.
5. Ignora entradas marcadas como `ReparsePoint` durante parte da leitura e antes da exclusão recursiva.
6. A limpeza calcula bytes liberados somente para arquivos removidos com sucesso e executa uma nova análise após o fluxo de limpeza.

## Limites e riscos atuais

| Severidade | Lacuna | Consequência |
| --- | --- | --- |
| Alta | O pacote declarativo ainda não tem assinatura nem mecanismo de atualização autenticada. | Uma distribuição pública não pode confiar em regras alteradas sem verificação criptográfica. |
| Alta | Não há modelo de risco (`SAFE`, `REVIEW`, `ADVANCED`, `BLOCKED`). | A interface não comunica claramente o limite de cada ação. |
| Alta | Não há validação de raiz canônica + revalidação imediatamente antes de apagar. | A proteção atual contra junction/symlink reduz risco, mas não é defesa completa contra troca de caminho/TOCTOU. |
| Alta | Sem detecção de processos que usam cache. | Pode produzir bloqueios, resultados parciais ou afetar aplicativos abertos. |
| Média | Exceções são descartadas no medidor de arquivos. | Perde-se a explicação de dados incompletos e não há auditoria. |
| Média | Não existe cancelamento de scan/limpeza. | Operações longas não são controláveis pelo usuário. |
| Média | Não existe histórico, manifesto ou recibo persistente. | Não há prova local verificável do que foi avaliado e do que foi removido. |
| Média | Não há testes automatizados, fixtures, sandbox de integração nem testes de concorrência. | Qualquer mudança de regra pode regredir segurança sem detecção. |
| Média | Há repositório e solução locais, mas não há baseline commitado, targeting pack/SDK fixado ou pipeline de release. | A build local é verificável, mas ainda não é reprodutível para distribuição segura. |
| Baixa | O nome visual é provisório. | Não bloqueia o núcleo, mas bloqueia publicação até validação de marca. |

## Decisão de migração

O motor atual é pequeno e parcialmente reutilizável, mas a separação deve ocorrer antes de uma troca grande de interface. A recomendação é manter o executável WinForms como protótipo legado até existir um núcleo testado e uma interface adaptadora nova.

O roadmap sugere PySide6/QML apenas para um motor Python existente. Este projeto é C#, então adotar Python e QML agora criaria uma reescrita dupla sem benefício demonstrado. A trilha local proposta é C# com núcleo independente da GUI; a escolha entre WinForms temporário, WPF ou WinUI será feita somente depois de haver SDK moderno e protótipo visual validado.

## Linha de base funcional a preservar

- Analisar antes de limpar.
- Permitir desmarcar achados.
- Manter navegador, sessão, cookies, histórico, Downloads, saves e arquivos pessoais fora do escopo.
- Percorrer todos os discos locais elegíveis sem seguir links/junctions.
- Informar remoções, bytes efetivamente liberados e itens ignorados.
