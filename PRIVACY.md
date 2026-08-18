# Privacidade — CLNXR (nome de trabalho)

## Regra padrão

O CLNXR é local-first. Nesta entrega não há telemetria, login, sincronização, anúncio, upload de recibos ou envio de lista de arquivos.

## Dados locais

Após uma limpeza, o aplicativo grava um recibo JSON em `%LocalAppData%\CLNXR\Receipts`. O recibo contém identificador de sessão/plano, regra usada, categoria, caminho local, contagens, bytes de arquivos removidos, mensagens de salto, horários e hash de integridade.

Esses dados podem incluir o nome do perfil do Windows porque o caminho avaliado é necessário para auditoria local. Eles não são enviados automaticamente. Exportar ou compartilhar um recibo é decisão explícita do usuário.

## Escopo excluído

O catálogo não inclui cookies, credenciais, logins, histórico, sessões de navegador, Downloads, saves, documentos, fotos, vídeos ou dados pessoais. A exclusão desses dados não é habilitável pela interface.

## Limite desta política

Esta é uma política de desenvolvimento do projeto; não substitui revisão jurídica nem uma política pública aprovada para lançamento.

