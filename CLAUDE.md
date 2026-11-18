# Sistema Escolar

Sistema web para escolas. Módulos: matrícula, alunos, professores, calendário acadêmico e notas.
Quatro perfis de acesso: responsáveis, funcionários, professores e alunos. Cada perfil vê só o necessário.

## Stack
- C# / ASP.NET Core (.NET 10), Razor Pages
- EF Core + PostgreSQL (Npgsql 10.x). Pacotes EF/Npgsql devem ficar na versão 10.x (a 11 exige .NET 11)
- Modo escuro desde o início (tema por variáveis CSS, `data-tema` no `<html>`)

## Desempenho (regra)
- Deve rodar bem em computadores antigos
- HTML renderizado no servidor; JavaScript mínimo, sem frameworks/bibliotecas (sem Bootstrap, sem jQuery)
- CSS leve em `wwwroot/css/site.css`; máscaras em `wwwroot/js/site.js` via `data-mask="cpf|tel|data|nome"`
- Validação é feita no servidor (sem validação client-side)

## Forma de trabalho
- Estilo vibe coding: testar, errar, corrigir. Código simples primeiro, sem se preocupar com arquitetura
- Um módulo por vez, passos pequenos, código que roda a cada etapa
- Quando algo quebrar, explicar a causa em poucas linhas
- Respostas sempre em português, diretas e curtas, mostrando o código completo dos arquivos alterados

## Como rodar
- `dotnet run` (http://localhost:5006). O segredo do banco vem do user-secrets e só vale em Development
- Conexão (não versionada). Em cada PC:
  `dotnet user-secrets set "ConnectionStrings:Escola" "Host=100.96.172.97;Port=5432;Database=claude_testes;Username=lovbin;Password=<senha>"`
  Em produção: variável `ConnectionStrings__Escola`. Nunca commitar a senha
- Banco criado com `EnsureCreated` (sem migrations). Ele NÃO altera tabelas existentes: mudou o modelo, é preciso apagar as tabelas ou adotar migrations (fazer antes de ter dados reais)
- Ao rodar a DLL direto (sem `dotnet run`) a porta é 5000 e o ambiente é Production, sem user-secrets: definir `ASPNETCORE_ENVIRONMENT=Development`

## Git
- Remoto: http://100.96.172.97:3002/lovbin/Sistema-Escolar.git (Gitea pessoal, via Tailscale), branch `main`
- `git pull` ao começar, commit e `git push` ao terminar. Autenticação com token do Gitea (nunca gravar o token em arquivo do repositório)

## Estado atual
### Módulo Matrícula (pronto e testado)
- CRUD em `Pages/Matriculas/`. Lista com busca (ILike) por aluno/responsável e filtro de status
- Modelo em `Data/Matricula.cs`; validadores (Nome, Cpf com dígitos verificadores, Telefone) em `Data/Validacoes.cs`
- Regras decididas:
  - Toda matrícula nasce **Pendente**, ignorando o que vier do formulário. Só a edição muda o status. No futuro a liberação virá do financeiro (pagamento + contrato assinado), manual por funcionário ou automática
  - Todos os campos obrigatórios, exceto Observações
  - Data no formato dd/MM/yyyy (cultura pt-BR). CPF `000.000.000-00` (11 dígitos válidos). Telefone `(31)9xxxx-xxxx` (celular, 11 dígitos). Nomes só com letras
  - Série em lista fixa (enum `Serie`: Maternal, Pré I/II, 1º–9º ano, 1ª–3ª série do médio), para padronizar relatórios
  - Ano letivo: só o ano atual e o seguinte (padrão: o seguinte)
  - Responsável acadêmico e financeiro separados (owned types `Academico` e `Financeiro`). Se `MesmoResponsavel`, o financeiro é cópia do acadêmico, salva no servidor

### Pendências conhecidas
- Sem login nem perfis de acesso (qualquer um vê tudo)
- Dados do aluno e dos responsáveis estão dentro da matrícula. Separar quando fizer o módulo Alunos
- Testar visualmente no navegador as máscaras de JS (cobertura até agora foi por HTTP; o usuário testou e disse que está ok)
- Próximos módulos: login e perfis, Alunos, Professores, Calendário acadêmico, Notas, Financeiro

## Cuidados
- Rodar servidores de teste em background: guardar o PID e encerrar com `kill <pid>`. Não usar `pkill -f` com o nome da DLL, pois mata o próprio shell
- Dados de teste criados no banco compartilhado devem ser apagados ao final
