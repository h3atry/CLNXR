# CLNXR — Catálogo inicial de regras

Cada regra tem ID e versão. A fonte operacional está em `src/Clnxr.Platform.Windows/rules/windows.v1.json`, embutida no assembly e validada antes de ser exposta ao scanner. Caminhos são resolvidos apenas pelo adaptador Windows; a UI não recebe um campo para excluir um caminho arbitrário. O código também verifica localmente envelopes `clnxr.rules.signed.v1` com RSA/SHA-256 antes de materializar regras, mas o catálogo embutido continua sendo a única fonte usada pelo scanner até existir uma chave pública de produção e um canal autenticado.

## Regras personalizadas locais

A página **Regras** permite criar uma regra com pasta raiz, idade mínima, extensões, exclusões relativas e atribuição. A prévia enumera os arquivos antes de salvar e mostra apenas exemplos redigidos. O catálogo local usa o esquema `clnxr.custom-rules.v1` em `%LocalAppData%\CLNXR\Rules\custom-rules.v1.json`.

Essas regras são sempre `ADVANCED`, `unsigned` e ficam fora dos perfis padrão. Só são analisadas quando o usuário escolhe o perfil **Personalizado** e marca o ID. A raiz inteira do perfil pessoal, Downloads, dados protegidos, links/junctions/reparse points e escopos que falhem na prévia são recusados. A limpeza usa a lista explícita de arquivos da prévia e revalida cada item antes de remover. O arquivo local guarda a definição para o mesmo usuário, mas não é exportado nem compartilhado automaticamente pelo ZIP portátil.

| ID | Perfil | Risco | Alvo | Guarda principal |
| --- | --- | --- | --- | --- |
| `profile-temp-v1` | Seguro/Completo | SAFE | `%LocalAppData%\Temp` de perfis elegíveis | raiz permitida, dados protegidos e reparse points bloqueados. |
| `wer-report-archive-v1` | Seguro/Completo | SAFE | WER `ReportArchive`, apenas arquivos com 7 dias ou mais | mesma política de caminho e filtro de idade. |
| `wer-report-queue-v1` | Seguro/Completo | SAFE | WER `ReportQueue`, apenas arquivos com 7 dias ou mais | mesma política de caminho e filtro de idade. |
| `app-crash-dumps-v1` | Seguro/Completo | SAFE | `%LocalAppData%\CrashDumps`, apenas arquivos com 7 dias ou mais | mesma política de caminho e filtro de idade. |
| `windows-temp-v1` | Completo | REVIEW | `%WINDIR%\Temp` | revisão manual; acessos negados são pulados. |
| `directx-cache-v1` | Completo | REVIEW | `%LocalAppData%\D3DSCache` | revisão manual. |
| `nvidia-*-cache-v1` | Completo | REVIEW | caches NVIDIA por perfil/sistema | revisão manual e sem encerrar processos. |
| `amd-*-cache-v1` | Completo | REVIEW | caches AMD por perfil | revisão manual e sem encerrar processos. |
| `intel-*-cache-v1` | Completo | REVIEW | caches Intel por perfil | revisão manual e sem encerrar processos. |
| `explorer-thumbnails-v1` | Completo | REVIEW | `thumbcache*.db` do Explorador | apenas padrão de arquivo conhecido. |
| `chrome-cache-v1` | Completo | REVIEW | subpastas `*Cache` de perfis do Chrome | protege perfil, Login Data, Cookies, History, sessões e Downloads; pula se `chrome` estiver aberto. |
| `edge-cache-v1` | Completo | REVIEW | subpastas `*Cache` de perfis do Edge | protege perfil, Login Data, Cookies, History, sessões e Downloads; pula se `msedge` estiver aberto. |
| `brave-cache-v1` | Completo | REVIEW | subpastas `*Cache` de perfis do Brave | protege perfil, Login Data, Cookies, History, sessões e Downloads; pula se `brave` estiver aberto. |
| `opera-cache-v1` | Completo | REVIEW | subpastas `*Cache` do Opera Stable | protege perfil, Login Data, Cookies, History, sessões e Downloads; pula se `opera` estiver aberto. |
| `firefox-cache-v1` | Completo | REVIEW | `cache2` dos perfis do Firefox | protege perfil, cookies, logins, histórico, sessões e Downloads; pula se `firefox` estiver aberto. |
| `unreal-derived-data-cache-v1` | Jogos | REVIEW | cache de dados derivados do Unreal Engine | pula quando `UnrealEditor` está aberto. |
| `nuget-http-cache-v1` | Desenvolvedor | REVIEW | `%LocalAppData%\NuGet\v3-cache` | pula quando `devenv` ou `dotnet` está aberto. |
| `npm-cache-v1` | Desenvolvedor | REVIEW | `%LocalAppData%\npm-cache` | pula quando `node` ou `npm` está aberto. |
| `pnpm-store-v1` | Desenvolvedor | REVIEW | `%LocalAppData%\pnpm\store` | pula quando `node` ou `pnpm` está aberto. |
| `yarn-cache-v1` | Desenvolvedor | REVIEW | `%LocalAppData%\Yarn\Cache` | pula quando `node` ou `yarn` está aberto. |
| `pip-cache-v1` | Desenvolvedor | REVIEW | `%LocalAppData%\pip\Cache` | pula quando Python/pip está aberto. |
| `uv-cache-v1` | Desenvolvedor | REVIEW | `%LocalAppData%\uv\cache` | pula quando `uv` ou Python está aberto. |
| `gradle-cache-v1` | Desenvolvedor | ADVANCED | `%USERPROFILE%\.gradle\caches` | exige revisão específica e pula quando Gradle/Java está aberto. |
| `maven-repository-v1` | Desenvolvedor | ADVANCED | `%USERPROFILE%\.m2\repository` | exige revisão específica e pula quando Maven/Java está aberto. |
| `cargo-registry-v1` | Desenvolvedor | ADVANCED | `%USERPROFILE%\.cargo\registry` | exige revisão específica e pula quando Cargo/Rust está aberto. |
| `discord-cache-v1` | Completo | REVIEW | subpastas `*Cache` do Discord | protege contas e configurações; pula se `discord` estiver aberto. |
| `teams-cache-v1` | Completo | REVIEW | subpastas `*Cache` do Microsoft Teams | protege contas e configurações; pula se Teams estiver aberto. |
| `epic-launcher-cache-v1` | Completo | REVIEW | `EpicGamesLauncher\Saved\webcache*` | protege bibliotecas, saves e configurações; pula se o launcher estiver aberto. |
| `spotify-cache-roaming-v1` / `spotify-cache-local-v1` | Completo | REVIEW | `Spotify\Storage` em AppData Roaming/Local | protege conta, preferências e downloads; pula se `spotify` estiver aberto. |
| `electron-cache-v1` | Completo | REVIEW | somente `%LocalAppData%\electron\Cache` | caminho delimitado; não tenta inferir perfis de aplicativos Electron. |

## Regras inexistentes por decisão

Não existem regras para cookies, histórico, login, Downloads, arquivos pessoais, registro, serviços, drivers, `WinSxS`, Windows Installer ou exclusão direta de Windows Update. As regras de navegador existentes apontam somente para subpastas de cache conhecidas e não autorizam o perfil inteiro.

## Processo de mudança

Uma nova regra precisa de ID estável, nova versão quando altera comportamento, justificativa, risco, guardas, testes em fixture normal/bloqueada, teste de reparse point, teste de acesso negado e teste de arquivo em uso quando aplicável. Sem esses itens, a regra não entra em nenhum perfil.
