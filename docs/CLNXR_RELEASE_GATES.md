# CLNXR — Gates de release

## P0 local atual

- [x] Auditoria do protótipo e arquitetura-alvo documentada.
- [x] Domínio, política de segurança, executor e recibos separados da UI.
- [x] Testes de sandbox de política, limpeza, recibo, cancelamento e processo em uso.
- [x] Interface provisória conectada a análise, revisão, limpeza, cancelamento, recibo e nova análise.
- [x] Smoke não visual em STA constrói e descarta a janela desktop sem exibi-la, analisar ou limpar.
- [x] Solução local com assemblies separados e build Release executado pelo MSBuild clássico; os executáveis de teste carregaram as dependências físicas.
- [ ] Targeting pack/SDK fixado e build sem avisos de referências ou arquitetura.
- [x] Build local do executável de desenvolvimento.
- [x] Grade de resultados virtualizada, com seleção/filtros preservados e smoke não visual do contrato.
- [x] Preferência local de movimento reduzido sanitizada e persistida sem telemetria.
- [x] Pacote ZIP local com executável, política de privacidade, versão, SBOM de desenvolvimento e checksum do executável.
- [ ] Teste visual interativo do build novo.
- [x] Testes com junction NTFS real, alvo trocado por junction (TOCTOU), symlink separado, arquivo real em uso e cancelamento antes da ação.
- [ ] Testes com ACL negada.
- [x] Cancelamento no meio de árvore de fixture para análise P1 somente leitura, sem remoção de arquivos.

## P1 funcional ainda pendente

- [x] Ferramenta de Lixeira: consulta global somente leitura, confirmação independente, API oficial do Windows e recibo local.
- [ ] Teste de integração de esvaziamento em fixture/VM; o esvaziamento real não foi executado nesta máquina.
- [x] Atalho confirmado para Storage Sense oficial; o CLNXR não altera suas políticas nem reporta limpeza como própria.
- [ ] Limpeza oficial do Windows apenas por API/ferramenta oficial.
- [x] Regras iniciais de Jogos (Unreal), Desenvolvedor (NuGet, npm, pnpm, Yarn, pip, uv, Gradle, Maven e Cargo) e caches de apps delimitados (Discord, Teams, Epic, Spotify e Electron), todas `REVIEW`/`ADVANCED` e desmarcadas por padrão.
- [x] Catálogo Windows migrado para pacote declarativo versionado, embutido e validado antes do uso; IDs duplicados e schema inválido falham a carga.
- [x] Regras personalizadas locais com prévia antes de salvar, escopo por pasta/idade/extensão/exclusão, risco `ADVANCED`, assinatura `unsigned`, persistência versionada e limpeza por itens explícitos revalidados.
- [ ] Pacote de regras assinado e atualizado por canal autenticado.
- [x] Motor somente leitura para mapa de disco, arquivos grandes e grupos duplicados, com cancelamento, limite de candidatos e exclusão de reparse points.
- [x] Interface provisória compilada expõe Mapa de disco, Arquivos grandes e Duplicados como ações explícitas; duplicados exige confirmação antes do hash.
- [x] Interface provisória compilada expõe Explorador de inicialização com desabilitação/restauração reversível somente para `HKCU` Run/RunOnce, além do Inspetor de arquivos bloqueados somente leitura; `HKLM`, pastas, processos e comandos permanecem fora da mutação.
- [x] Inventário de resíduos de desinstalação somente leitura baseado em `InstallLocation` declarado, sem remoção de Registro ou execução de desinstaladores.
- [x] Agendamento opt-in do perfil Seguro com horário/argumentos fixos, sem elevação automática, recibo local e remoção explícita.
- [x] Network Utilities com diagnóstico `/all`, Flush DNS confirmável e planos manuais explícitos para resets que exigem elevação/reinicialização.
- [x] System Repair Hub com SFC `/verifyonly`, DISM `/CheckHealth` e CHKDSK `/scan`; switches destrutivos não são executados automaticamente.
- [x] Contratos P3 cobertos no sandbox, incluindo catálogo fechado, volume validado, recusa de switches/caminhos arbitrários e não execução silenciosa de reset.
- [ ] Teste visual e teste de desempenho das ferramentas P1 em volumes grandes.
- [ ] Testes de integração por launcher/jogo/ambiente de desenvolvimento em VM antes de ampliar o catálogo.
- [x] Filtros e virtualização de resultados para catálogos grandes (contrato local; benchmark de volume ainda pendente).
- [x] Exportação explícita de recibos JSON locais e verificação de integridade SHA-256 no histórico.
- [x] Recibos novos declaram o esquema `clnxr.receipt.v1` e a verificação local exige a versão e o hash.
- [x] Visualização estruturada somente leitura do conteúdo atual, com status de integridade SHA-256.
- [x] CLI separado com dry-run padrão, seleção explícita, códigos de saída e relatório JSON redigido.
- [ ] Migração formal de esquemas futuros de recibo.

## Gates externos para beta público / 1.0

O repositório público e os pré-releases técnicos `v0.1.0-dev.5`/`v0.1.0-dev.6` existem em `https://github.com/h3atry/CLNXR`; isso não equivale a beta público nem atende os gates abaixo.

- [ ] Nome, marca, domínio e identificadores públicos verificados juridicamente.
- [ ] Certificado de assinatura de código e chave de assinatura sob controle apropriado.
- [ ] Máquina virtual limpa e matriz de Windows suportada.
- [ ] Auditoria de segurança independente e testes antivírus/SmartScreen.
- [ ] SBOM de release, checksums e canal de distribuição controlado.
- [ ] Política de privacidade e termos revisados para publicação.
- [ ] Beta externo com consentimento e canal de relatórios.
- [ ] Atualizador assinado, estratégia de rollback e plano de resposta a incidente.

## Conclusão honesta

O roadmap pode ser concluído localmente até os itens sob controle do projeto. “Versão 1.0 pública” não pode ser declarada enquanto qualquer gate externo acima estiver pendente. Assinatura, verificação de marca, VM limpa e beta não são substituíveis por código local.
