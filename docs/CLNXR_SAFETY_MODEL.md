# CLNXR — Modelo de segurança

## Invariantes não negociáveis

1. Analisar não altera arquivos.
2. Limpar só executa um `ActionPlan` revisado e confirmado pelo usuário.
3. Ações são limitadas a regras conhecidas e catalogadas; não há exclusão livre por caminho digitado.
4. Cada alvo é validado de novo imediatamente antes de ser removido.
5. Junctions, symlinks, outros reparse points e qualquer escape da raiz aprovada são bloqueados.
6. Não matar processo para “liberar” cache. Arquivo em uso é pulado com motivo.
7. Todo resultado é gravado em recibo local, inclusive falhas, cancelamentos e itens pulados.

## Dados permanentemente protegidos

O catálogo não pode criar regra de limpeza para:

- cookies, credenciais, perfis, logins, histórico ou dados de sessão de navegador;
- Downloads, documentos, fotos, vídeos, área de trabalho, saves e dados pessoais;
- `WinSxS`, cache do Windows Installer, Component Store ou exclusão direta de arquivos de Windows Update;
- registro do Windows, drivers, serviços, inicialização ou configurações de segurança;
- destinos resolvidos fora da raiz canônica da regra.

Os perfis de navegador continuam protegidos como um todo. A exceção permitida é uma subpasta de cache conhecida (`Cache`, `Code Cache`, `GPUCache`, `DawnCache`, `ShaderCache` ou `cache2`) quando uma regra REVIEW aponta diretamente para ela e exige o processo correspondente fechado. Isso não autoriza `Login Data`, `Cookies`, `History`, sessões, Downloads ou qualquer arquivo irmão.

Limpeza de componentes do Windows Update ou Delivery Optimization só pode existir por API/ferramenta oficial, nunca por deleção direta.

## Classificação de risco

| Risco | Regra de produto |
| --- | --- |
| `SAFE` | Alvo conhecido, escopo fechado, sem impacto funcional esperado; pode vir selecionado no perfil Seguro. |
| `REVIEW` | Exige processo fechado, privilégio ou consequência a ser explicada; vem desmarcado. |
| `ADVANCED` | Regra opcional, destinada a usuário que entende o impacto; nunca é padrão. |
| `BLOCKED` | Proibida pelo produto, mesmo que o caminho exista. |

## Guardas por descoberta e por ação

Uma regra deve verificar, no mínimo:

1. O caminho candidato foi obtido de uma raiz permitida, normalizada e canônica.
2. O caminho continua dentro da raiz de regra após resolução segura.
3. Nenhum componente do caminho é reparse point.
4. A regra e o perfil permitem a ação.
5. O alvo não pertence a uma área protegida.
6. O processo relacionado está fechado quando a regra exige isso.
7. Filtros de idade são aplicados tanto na estimativa quanto na remoção; arquivos recentes são preservados e contabilizados como pulados.
8. O alvo existe e continua com a mesma identidade/escopo medido antes da exclusão.

Falhar em qualquer guarda produz `Blocked` ou `Skipped`, nunca tentativa de exclusão por força.

## Regras declarativas

Cada regra possui, no mínimo:

```text
rule_id
version
category
risk
roots permitidas
matcher de alvo
guardas
ação permitida
explicação ao usuário
processos relacionados
testes/fixtures obrigatórios
```

Alterar uma regra exige nova versão, revisão de segurança e testes de caminho normal, bloqueado, reparse point, acesso negado e arquivo em uso.

## Evidência e privacidade

- Por padrão não há telemetria nem envio de lista de arquivos.
- O recibo local minimiza dados pessoais: registra regra, categoria, unidade, resultado, contagens, bytes, tempos e mensagens sanitizadas.
- Exportação só ocorre por ação explícita do usuário.
- Hashes não substituem validação de caminho; eles complementam integridade do recibo quando aplicável.

## Critérios de rejeição

Uma implementação não é aprovada se fizer qualquer uma destas coisas:

- seguir link/junction durante análise ou limpeza;
- apagar arquivo fora da raiz da regra;
- limpar dados protegidos “por heurística”;
- elevar privilégio automaticamente ou matar processo;
- informar sucesso sem registrar resultado por alvo;
- tratar varredura incompleta como limpeza completa.
