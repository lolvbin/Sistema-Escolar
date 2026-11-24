# Guia de deploy e backup

Para colocar o Sistema Escolar em produção. Serve a **uma instância só** (ver "Limites" no fim).

## O que você precisa
- Um servidor Linux com Docker (ou o .NET 10 runtime) e um PostgreSQL 15+ (no mesmo servidor ou gerenciado).
- Um domínio com **HTTPS** (certificado válido). O login usa cookie `Secure`: sem HTTPS ninguém consegue entrar.

## 1. Banco de dados
Crie um banco e um usuário só para a escola (não use o superusuário):

```sql
CREATE USER escola WITH PASSWORD 'uma-senha-forte';
CREATE DATABASE escola OWNER escola;
```

Não precisa criar tabelas: o sistema aplica as migrations sozinho ao subir (`Migrate()`). Em toda atualização com mudança de modelo, ele atualiza o banco na partida, então **faça um backup antes de atualizar** (item 5).

## 2. Variáveis de ambiente

| Variável | Valor | Para quê |
|---|---|---|
| `ConnectionStrings__Escola` | `Host=...;Port=5432;Database=escola;Username=escola;Password=...` | Conexão (obrigatória). Nunca commitar |
| `ASPNETCORE_ENVIRONMENT` | `Production` (padrão na imagem) | Em Development o sistema cria usuários de teste com senha conhecida: **jamais em produção** |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` (já na imagem) | O app enxerga que o acesso é HTTPS por trás do proxy |
| `DATAPROTECTION_DIR` | `/chaves` (já na imagem) | Pasta **persistente** com as chaves do login. Sem ela, cada reinício desloga todo mundo |
| `Escola__CookieSeguro` | `false` só para teste em http local | Em produção deixe sem definir (cookie sempre `Secure`) |

## 3. Subir com Docker

```bash
docker build -t sistema-escolar .
docker run -d --name escola --restart unless-stopped \
  -p 127.0.0.1:8080:8080 \
  -e "ConnectionStrings__Escola=Host=db;Database=escola;Username=escola;Password=SENHA" \
  -v escola-chaves:/chaves \
  sistema-escolar
```

O app escuta em HTTP na porta 8080; o HTTPS fica no proxy reverso. Exemplo com Caddy (certificado automático):

```
escola.exemplo.com.br {
    reverse_proxy 127.0.0.1:8080
}
```

(Nginx também serve: repasse `X-Forwarded-For` e `X-Forwarded-Proto`.)

Sem Docker: `dotnet publish -c Release -o publicar`, e rode `dotnet publicar/SistemaEscolar.dll` com as mesmas variáveis (serviço systemd, por exemplo). Defina `DATAPROTECTION_DIR` para uma pasta que o serviço consiga gravar.

## 4. Primeiro acesso
Com o banco vazio, abrir o site leva à tela **Primeiro acesso**: crie o administrador (senha de 8+ caracteres com letra e número). A tela some assim que existe um administrador. Depois, em **Usuários**, cadastre os demais funcionários; professores, alunos e responsáveis são criados nas próprias telas. Em **Configurações**, ajuste nome da escola, médias, frequência, multa e juros. Troque o logo colocando `wwwroot/img/logo.svg` (ou `.png`) na imagem/publicação.

Conferência rápida depois de subir:
- `https://seu-dominio/saude` responde `ok` (200); 503 = o app não alcança o banco. Use esse endereço no monitoramento.
- Entre, saia e entre de novo; reinicie o contêiner e confirme que a sessão continua (chaves persistentes).

## 5. Backup do PostgreSQL
O script `scripts/backup.sh` faz um `pg_dump` (formato custom), confere se o arquivo é legível e apaga os mais antigos que N dias:

```bash
PGHOST=localhost PGUSER=escola PGPASSWORD=SENHA PGDATABASE=escola \
  ./scripts/backup.sh /var/backups/escola 30
```

Agende no cron (todo dia às 2h) e **copie os arquivos para outro lugar** (outro servidor, nuvem, disco externo): backup só no mesmo disco não protege contra perda do servidor.

```
0 2 * * * PGHOST=localhost PGUSER=escola PGPASSWORD=SENHA PGDATABASE=escola /opt/escola/scripts/backup.sh /var/backups/escola 30
```

**Restaurar** (em um banco vazio; teste isso ao menos uma vez antes de precisar):

```bash
createdb -O escola escola_restaurado
pg_restore --no-owner --role=escola -d escola_restaurado /var/backups/escola/escola_AAAAMMDD_HHMMSS.dump
```

Aponte `ConnectionStrings__Escola` para o banco restaurado e suba o app. Faça também um backup antes de cada atualização do sistema.

## 6. Atualizar o sistema
1. Backup (item 5).
2. `git pull`, `docker build -t sistema-escolar .`, recrie o contêiner com o mesmo comando do item 3. As migrations rodam na partida.
3. Confira `/saude` e entre no sistema.

## 7. Lista de segurança antes de abrir para a escola
- [ ] HTTPS funcionando e o app só acessível pelo proxy (porta 8080 presa em `127.0.0.1`).
- [ ] `ASPNETCORE_ENVIRONMENT` = `Production` (nenhuma conta `@escola.test`).
- [ ] Senha do banco forte e fora do repositório; usuário do banco sem superpoderes.
- [ ] Backup diário agendado, cópia fora do servidor e restauração testada.
- [ ] Mais de um administrador cadastrado (o sistema impede ficar sem nenhum, mas ter reserva evita bloqueio).
- [ ] Servidor com atualizações de segurança e firewall liberando só 80/443 (e SSH).

## Limites desta versão (ver `CLAUDE.md`, "Limites e pendências")
- **Uma instância só:** configurações, bloqueio de login por tentativas e lista de sessões revogadas ficam em memória. Reiniciar o servidor zera o bloqueio de login e a lista de sessões revogadas.
- Não há "esqueci minha senha" por e-mail: o funcionário com a área Usuários gera uma senha provisória.
- Não há envio de e-mail, boleto/Pix real nem integração de pagamento.
