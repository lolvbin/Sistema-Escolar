# Sistema Escolar

Sistema web para escolas. Módulos: matrícula, alunos, professores, disciplinas, turmas, notas e frequência, calendário, financeiro e configurações.
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
  `dotnet user-secrets set "ConnectionStrings:Escola" "Host=<host>;Port=5432;Database=<banco>;Username=<usuario>;Password=<senha>"`
  Em produção: variável `ConnectionStrings__Escola`. Nunca commitar a senha
- **Banco com EF Migrations** (`Migrations/`): `Program.cs` chama `Migrate()` na partida (cria/atualiza o banco). Mudou o modelo? `dotnet ef migrations add NomeDaMudanca` e commitar a pasta `Migrations/`. `Data/EscolaContextFactory.cs` deixa o `dotnet ef` funcionar sem subir a aplicação (lê user-secrets ou `ConnectionStrings__Escola`). Ferramenta: `dotnet tool install --global dotnet-ef --version 10.*`
- O banco de dev compartilhado foi criado antes das migrations: ele foi "baselinado" (linha `InitialCreate` inserida em `__EFMigrationsHistory`). Banco novo = `Migrate()` cria tudo. As menções abaixo a "tabela criada por SQL em ...Preparar" são HISTÓRICAS: esses métodos foram removidos e o esquema vem das migrations
- Testes: `dotnet test tests/SistemaEscolar.Tests` (xUnit, 115 de regras puras: validações, aprovação/média/recuperação, frequência, multa e juros, liberação da matrícula, limite de login). 6 testes de integração usam PostgreSQL real (criam e apagam um banco temporário; 3 de migrations/modelo e 3 de **permissão por HTTP** em `PermissaoTests`, que sobem o app com `WebApplicationFactory` e conferem página × perfil/cargo, POST barrado e sessão revogada) e só rodam com `ESCOLA_TEST_DB="Host=...;Database=postgres;Username=...;Password=..."`; sem a variável são ignorados
- `docker compose up -d` sobe um PostgreSQL local (usuário/senha/banco `escola`/`escola_dev`/`escola`)
- Ao rodar a DLL direto (sem `dotnet run`) a porta é 5000 e o ambiente é Production, sem user-secrets: definir `ASPNETCORE_ENVIRONMENT=Development`

## Git
- Remotos: `origin` (Gitea pessoal do autor, privado) e `github` (público), branch `main`. Os dois recebem o mesmo código
- `git pull` ao começar, commit e `git push` ao terminar. Autenticação por token (nunca gravar token ou senha em arquivo do repositório)

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
  - Responsável acadêmico e financeiro (owned types `Academico` e `Financeiro`) agora ficam no `Aluno`. Se `MesmoResponsavel`, o financeiro é cópia do acadêmico, salva no servidor

### Login e perfis (testado no navegador)
- `Data/Usuario.cs`: entidade `Usuario`, enum `Perfil` (Responsavel, Funcionario, Professor, Aluno), hash PBKDF2 (`Senha`) e `UsuarioSeed`
- Cookie auth em `Program.cs`; política padrão exige login. `[AllowAnonymous]` em Login, Error. Pasta `/Matriculas` só para quem tem a área Matrículas (ver "Funcionários: cargos e áreas")
- A tabela `Usuarios` vem das migrations; `UsuarioSeed.Semear` só cria as contas de teste
- Em Development cria 4 usuários `<perfil>@escola.test` (senha de dev em `UsuarioSeed`). Não vale em produção: criar usuários reais por outro meio
- **Navegação**: o topo só tem marca (com logo opcional), Sair e Tema. O único menu são os cartões da tela inicial (`Pages/Shared/_Painel.cshtml`), um conjunto por perfil, com as telas que ele pode acessar + "Alterar senha". Página nova = adicionar o cartão no perfil certo. A prévia estática (`/Previa`) foi removida
- **Logo**: coloque `wwwroot/img/logo.svg` (ou `logo.png`); aparece sozinha ao lado do nome no topo (altura 30px)
- **Limite de tentativas** (`Data/Autenticacao.cs`, `LimiteLogin`): 5 falhas por e-mail ou 30 por IP bloqueiam por 15 min (mensagem com o tempo restante, nem confere a senha). Fica em memória: reiniciar o servidor zera. Vale também para errar a senha atual na troca
- **Trocar a própria senha**: `Pages/Conta/Senha` (menu "Alterar senha"; mínimo 8 caracteres com letra e número, diferente da atual). Senha provisória (professor novo, "Nova senha", acessos de aluno/responsável) marca `Usuario.DeveTrocarSenha`: no login o claim `trocar-senha` redireciona (middleware em `Program.cs`) para a troca até ela ser feita. Usuários de dev (`@escola.test`) não são forçados
- Cookie/claims criados por `Autenticacao.CriarPrincipal`

### Alunos e Matrícula (fluxo: matrícula primeiro, testado no navegador)
- REGRA ESSENCIAL: não existe cadastrar aluno sem matrícula. O aluno é criado **junto** com a matrícula, em `Pages/Matriculas/Create` (um formulário só: dados do aluno + responsáveis + série/turno/ano). Não há mais `Alunos/Create`
- Rematrícula: se o CPF já existe e o nome confere, reaproveita o aluno (atualiza data de nascimento e responsáveis) e cria a matrícula do novo ano. CPF de outro nome ou aluno já matriculado no ano: erro
- Excluir a última matrícula de um aluno exclui o aluno junto (aluno só existe com matrícula)
- `Pages/Alunos/` é só consulta (lista com busca) e correção dos dados (`Edit`), só Funcionário. Matrícula > Editar tem link para os dados do aluno; na edição da matrícula o aluno não muda
- Modelo: `Data/Aluno.cs` (nome, nascimento, CPF único, responsáveis acadêmico/financeiro, `MesmoResponsavel`) e `Matricula` (`AlunoId` FK Restrict, série, turno, ano, status, data, observações). Índice único `AlunoId + AnoLetivo`
- Partials compartilhados em `Pages/Shared`: `_DadosAluno` (aluno + responsáveis) e `_Responsavel`; `Pages/Matriculas/_Form` tem só os campos da matrícula. No Create os campos têm prefixo `Aluno.` e `Matricula.` (`Validar(ModelState, "prefixo")`)
- A migração do formato antigo de matrícula foi removida (não há mais dados nesse formato)

### Módulo Professores (testado no navegador)
- `Data/Professor.cs` + `Pages/Professores/` (lista com busca e filtro ativo/inativo, novo, editar), só Funcionário. Tabela `Professores` criada por SQL em `TabelaProfessores.Preparar` (depois de `Usuarios`)
- Ao cadastrar, cria também o login (`Usuario`, perfil Professor) com o mesmo e-mail e uma **senha provisória** mostrada uma única vez (TempData). Editar nome/e-mail do professor atualiza o login
- Professor não é excluído: **Inativar** bloqueia também o login; **Nova senha** gera outra provisória. Não existe ainda tela para o usuário trocar a própria senha
- Todos os campos obrigatórios (nome, nascimento 18+, CPF único, telefone, e-mail único, formação)

### Módulo Disciplinas (testado no navegador)
- `Data/Disciplina.cs` + `Pages/Disciplinas/` (lista com busca e filtro, nova, editar, inativar), só Funcionário. Tabelas `Disciplinas` e `DisciplinaSeries` (disciplina × série, por enum `Serie`) criadas por SQL em `TabelasDisciplinas.Preparar`
- Cada disciplina marca as séries em que é oferecida (pelo menos uma). Não há exclusão, só inativar. Nome único, só letras
- Botão "Carregar disciplinas padrão" (só aparece com a lista vazia) cria um catálogo inicial (`TabelasDisciplinas.Padrao()`), editável. O banco de dev já tem as 16 padrão

### Módulo Turmas (testado no navegador)
- `Data/Turma.cs` + `Pages/Turmas/` (lista com filtro por ano/série, nova, detalhes), só Funcionário. Tabelas `Turmas` e `TurmaDisciplinas` (+ coluna `Matriculas.TurmaId`) criadas por SQL em `TabelasTurmas.Preparar`
- Turma = série + ano letivo + letra (A, B...) com um turno. Única por ano+série+letra. Nome exibido: "5º ano A"
- Enturmação pela matrícula (`Matricula.TurmaId`): só entra aluno com matrícula **Ativa**, mesma série, ano e turno, ainda sem turma. Remover tira o aluno da turma
- Cada disciplina ativa da série ganha um professor ativo (`TurmaDisciplina`); salvar em lote na tela de detalhes
- Regras ligadas à matrícula: matrícula que deixa de ser Ativa sai da turma sozinha; mudar série/ano/turno de aluno enturmado é bloqueado (remover da turma antes)
- Turma pode ser inativada; só exclui turma sem alunos (professores atribuídos saem junto)
- Sempre mapear tabela de entidade sem DbSet com `ToTable("...")` (o EF usa o nome da classe no singular; com migrations o nome da tabela sai do mapeamento, então mantenha o plural explícito)

### Módulo Notas (testado no navegador)
- `Data/Nota.cs` (chave composta MatriculaId + DisciplinaId + Bimestre, valor 0 a 10 com 1 casa) e `TabelaNotas.Preparar` (SQL, depois de Turmas). 4 bimestres fixos (`TabelaNotas.Bimestres`)
- `Pages/Notas/` só Professor (política `Professor`): Index lista as disciplinas/turmas ativas atribuídas a ele (`TurmaDisciplina.ProfessorId`, ligado pelo `Professor.UsuarioId` = claim NameIdentifier); `Lancar` = notas de uma turma/disciplina/bimestre, só alunos com matrícula Ativa na turma
- Aceita vírgula ou ponto, arredonda para 1 casa; campo vazio apaga a nota; se um valor for inválido nada é salvo. Professor que não é o da disciplina: Acesso negado
- Visão do funcionário: ver "Desempenho" abaixo

### Boletim e acessos (testado no navegador)
- `Pages/Boletim` para Aluno e Responsável (política `Boletim`, menu "Boletim"): disciplinas da turma × 4 bimestres + média das notas já lançadas (sem regra de aprovação ainda). Mostra a matrícula mais recente com turma
- Ligação: aluno por `Aluno.UsuarioId` (coluna criada em `TabelaNotas.Preparar`); responsável pelo **e-mail** (acadêmico ou financeiro) igual ao do login. Só vê os próprios alunos
- Funcionário cria os acessos em `Alunos/Edit` ("Acessos ao sistema"): aluno (informa e-mail) e responsável (e-mail já cadastrado); senha provisória mostrada uma vez
- Falta: visão do funcionário/professor, trocar a própria senha

### Desempenho (visão do funcionário, testado no navegador)
- `Pages/Desempenho/` só Funcionário (cartão "Notas e frequência"), só leitura: `Index` (turmas com filtro ano/série), `Turma/{id}` (alunos × disciplinas com média final, frequência geral e situação) e `Aluno/{matrículaId}` (boletim completo do aluno)
- Cálculo compartilhado em `Data/ResultadoAluno.cs` (`ResultadoAluno.Calcular`), usado também pelo `Boletim`. NÃO chamar de `Desempenho` (o namespace `Pages.Desempenho` esconde a classe)
- Falta: visão do professor sobre as próprias turmas além do lançamento, exportação/impressão

### Módulo Calendário (testado no navegador)
- `Data/Evento.cs` (título, tipo, início, fim opcional, descrição; vale para a escola toda) e `TabelaEventos.Preparar` (SQL). Tipos: Evento, Feriado, Recesso, Prova, Reunião
- `Pages/Calendario/Index`: grade mensal (domingo a sábado) + lista do mês, navegação anterior/próximo, visível a todos os perfis logados (menu "Calendário"). `Pages/Calendario/Evento` (novo/editar/excluir) só Funcionário
- Erros de validação em `[BindProperty] Evento` precisam da chave com prefixo (`"Evento.Fim"`). Propriedades calculadas do modelo levam `[NotMapped, ValidateNever]` (senão o validador as lê e quebra com nulo)
- Falta: eventos por turma/série, ligar provas às turmas, início/fim dos bimestres

### Aprovação e média (testado no navegador)
- `Data/Aprovacao.cs`: limites **configuráveis** em `Pages/Configuracoes` (só Funcionário, menu "Configurações"). Ficam na tabela `Configuracoes` (linha única Id=1, criada por `TabelaConfiguracoes.Preparar`) e em propriedades estáticas carregadas na partida e atualizadas ao salvar (app de uma instância só). Média anual das 4 notas (arredonda 1 casa): **>= 6,0 Aprovado; 4,0 a 5,9 Recuperação; < 4,0 Reprovado**. Faltando nota em algum bimestre: "Em andamento"
- Situação geral do aluno: Em andamento se alguma disciplina está em andamento; senão Reprovado > Recuperação > Aprovado
- Aparece no `Boletim` (por disciplina e geral) e na tela `Notas/Lancar` (média anual e situação por aluno). Estilo `.sit-*` no CSS
- Recuperação deve ser menor que a aprovação (0 a 10, vírgula ou ponto). Mudar vale na hora para todos os boletins
- **Recuperação**: nota guardada em `Notas` como bimestre 5 (`TabelaNotas.BimRecuperacao`). Só quem tem as 4 notas e média entre a de recuperação e a de aprovação entra (aba "Recuperação" em `Notas/Lancar`). A média final depende do **modelo escolhido pela escola** em Configurações (`ModoRecuperacao`, coluna `Configuracoes.ModoRecuperacao`): 0 média das duas (padrão), 1 maior das duas, 2 recuperação substitui a média. Em todos precisa atingir a média de aprovação; senão Reprovado. Novo modelo = novo valor no enum + um caso em `Aprovacao.MediaFinal`
- **Frequência**: professor lança as **aulas dadas** por turma/disciplina/bimestre (`AulasDadas`) e as **faltas** por aluno (`Frequencias`) na mesma tela de notas. Faltas não passam das aulas dadas. Frequência % = 1 - faltas/aulas. Mínimo configurável (padrão 75%, coluna `Configuracoes.FrequenciaMinima`): frequência **geral** abaixo disso reprova o aluno (só depois de todas as disciplinas definidas). Boletim mostra Rec., Média final, Freq. por disciplina e geral
- Falta: abono/justificativa de faltas, frequência diária (chamada), regra de recuperação por bimestre
- Cuidado nos testes: se `dotnet run` falhar em silêncio, pode haver servidor antigo na porta 5006 (`ss -ltnp | grep 5006`): encerrar pelo PID

### Módulo Financeiro (testado no navegador)
- `Data/Financeiro.cs`: `Cobranca` (tipo Matrícula/Mensalidade, número, vencimento, valor, `PagoEm`, forma), `TabelaFinanceiro.Preparar` (SQL; também cria `Matriculas.ContratoAssinadoEm`) e `RegrasFinanceiro` (ler valor "1.250,50", ler data dd/MM/yyyy, `TentarLiberar`). A classe de regras NÃO pode se chamar `Financeiro`: o namespace `Pages.Financeiro` a esconde
- `Pages/Financeiro/` só Funcionário: `Index` (lista com busca e filtros: em atraso, pendente, sem contrato, sem cobranças; mostra em aberto e atrasado) e `Detalhe/{matrículaId}`: registrar contrato assinado, gerar cobranças (taxa opcional + 1 a 12 mensalidades, vencimento mensal a partir da 1ª), dar baixa (data não futura + forma: Pix, Dinheiro, Cartão, Boleto, Transferência), desfazer pagamento, apagar cobranças em aberto
- **Liberação da matrícula**: Pendente vira Ativa sozinha quando o contrato está assinado **e** a taxa de matrícula está paga (`TentarLiberar`, chamada após baixa e após registrar contrato). Também há "Liberar manualmente". Desfazer pagamento não volta o status
- Matrícula com pagamento registrado não pode ser excluída (cancelar em vez disso); sem pagamento, excluir leva as cobranças junto
- `Pages/Mensalidades` (política `Responsavel`, menu "Mensalidades"): responsável vê as cobranças dos alunos ligados ao e-mail dele (acadêmico ou financeiro), só leitura
- Atrasada = não paga e vencida antes de hoje. **Multa e juros** (`Cobranca.Encargos`): multa única de X% do valor + juros simples de Y% ao mês proporcionais aos dias (padrão 2% e 1%, configuráveis em Configurações, colunas `Configuracoes.MultaPercentual/JurosMensal`). Calculados na hora para cobranças em aberto; na baixa o valor recebido é gravado em `Cobranca.ValorPago` (histórico não muda se as taxas mudarem). Checkbox "Isentar multa e juros" na baixa. Sem desconto, sem boleto/Pix real (baixa é manual)
- **Inadimplência** (`Pages/Financeiro/Inadimplencia`, botão no Financeiro): uma linha por aluno com parcelas vencidas e não pagas, com multa e juros de hoje; filtros por ano e faixa de atraso (até 30, 31 a 60, mais de 60 dias), resumo no topo, botão Imprimir (CSS `@media print` esconde o topo e `.no-print`)
- **Recibo** (`Pages/Financeiro/Recibo/{cobrançaId}`, link "Recibo" nas cobranças pagas do Detalhe): só de cobrança paga (senão 404), com valor por extenso (`Extenso.Reais` em `Data/Financeiro.cs`), multa e juros separados, para imprimir. Só funcionário por enquanto (responsável ainda não emite o próprio recibo)
- Falta: descontos/bolsas, integração com pagamento online, recibo no acesso do responsável

### Funcionários: cargos e áreas (testado no navegador)
- `Data/Areas.cs`: enum de flags `Area` (Matrículas, Alunos, Professores, Disciplinas, Turmas, Desempenho, Financeiro, Calendário, Configurações, Usuários; todas = 1023) e enum `Cargo` (Administrador/TI, Secretaria, Financeiro, Coordenação). O cargo é só um **atalho**: `Cargo.Padrao()` preenche as áreas ao criar; o que vale é `Usuario.Areas`. Acesso por área é tudo ou nada (não há só leitura)
- Cargos: Administrador = todas; Secretaria = Matrículas, Alunos, Professores, Disciplinas, Turmas, Desempenho, Calendário; Financeiro = Financeiro, Calendário; Coordenação = Professores, Disciplinas, Turmas, Desempenho, Calendário. Só o administrador tem Configurações e Usuários
- Cada área tem uma política `Area:<nome>` (`Program.cs`); as pastas das telas usam essas políticas (a política antiga `Funcionario` não existe mais). As áreas vão no cookie (claim `areas`, `User.TemArea(Area.X)`). Calendário: todos veem; só quem tem a área cadastra. Cartões da tela inicial só aparecem para quem tem a área
- `Pages/Usuarios/` (Index, Create, Edit com `_Form`): só quem tem a área Usuários; gerencia **funcionários** (professor/aluno/responsável continuam nas próprias telas). Cria com senha provisória (mostrada uma vez, troca obrigatória); Editar muda nome, e-mail, cargo, áreas e ativo; "Gerar nova senha provisória". Em `site.js`, o `select[data-preset]` do cargo marca as áreas sugeridas
- **Trava**: sempre sobra pelo menos um funcionário ativo com a área Usuários (`RegrasUsuarios.RestaAdministrador`)
- **Sessões**: `Sessoes.Revogar(id)` (em `Data/Autenticacao.cs`) derruba os cookies emitidos antes quando o usuário é inativado, muda de áreas/e-mail ou recebe nova senha (também ao inativar professor). Em memória: reiniciar o servidor zera a lista. Quem edita o próprio acesso é re-logado na hora
- **Primeiro acesso**: `Pages/PrimeiroAcesso` (anônima) só existe enquanto não há nenhum administrador ativo (senão 404); o Login redireciona para ela. Cria o Administrador e já entra. Em Development nunca aparece (a conta `funcionario@escola.test` é administradora)
- Migration `AreasDoFuncionario`: funcionários que já existiam viram Administrador (Areas = 1023)
- `--demo` cria também 3 funcionários (`secretaria|financeiro|coordenacao.demo@demo.test`, senha de dev); `--demo-limpar` remove
- Cuidado nos testes via `curl`: errar a senha 5 vezes para o mesmo e-mail bloqueia o login por 15 min (é o limite de tentativas funcionando)

### Sala de aula (testado no navegador)
- `Data/Publicacao.cs` (tabela `Publicacoes`, migration `SalaDeAula`): publicação de uma turma em uma disciplina, tipo Aviso, Material ou Atividade (com prazo), título, texto (até 2000), link opcional (só http/https), autor e data. Sem envio de arquivos e sem entrega de atividade
- `Pages/SalaDeAula/`: `Index` (professor vê as turmas/disciplinas dele; aluno e responsável leem o mural da turma do aluno, com filtro por disciplina) e `Turma/{turmaId}/{disciplinaId}` (só o professor atribuído àquela disciplina na turma: publicar e excluir; senão Acesso negado). Parcial `_Publicacao` mostra um item
- Política `SalaDeAula` = Professor, Aluno e Responsável (cartão "Sala de aula" nos três); `/SalaDeAula/Turma` exige Professor. `Acessos.AlunosDoUsuario` (em `Data/Publicacao.cs`) acha os alunos do aluno/responsável logado e o Boletim usa o mesmo
- Falta: ver limites abaixo

### Registro de ações (log de auditoria, testado no navegador)
- `Data/Auditoria.cs`: entidade `RegistroLog` (tabela `Registros`, migration `RegistroDeAcoes`: quando, usuário id/nome, ação, detalhe) e extensão `db.Registrar(User, Auditoria.X, "texto")`, que só adiciona ao contexto: grava no mesmo `SaveChanges` da ação (se der erro, nem a ação nem o log). Sem FK para usuário (o registro sobrevive a ele)
- Registra: baixa/estorno de pagamento, gerar/apagar cobranças, contrato assinado/removido, liberação manual e automática da matrícula, alterar/excluir matrícula, **notas alteradas** (só o que mudou, "aluno: 7,0 → 8,5"), mudanças de Configurações (antes → depois), funcionário criado/editado, nova senha provisória (funcionário e professor), inativar/reativar professor, criar acesso de aluno/responsável
- `Pages/Log/Index`: só quem tem a área Configurações (administrador); filtros por ação, texto, período; mostra os 200 mais recentes; sem editar/apagar. Cartão "Registro de ações" na tela inicial
- Falta: registrar frequência/faltas e publicações da sala de aula; login/logout e tentativas bloqueadas; exportar; política de retenção (nunca apaga)

### Documentos para imprimir (testado no navegador)
- Todos usam `window.print()` + CSS `@media print` (`.no-print` some, `.so-print` só aparece impresso, `.doc*` em `site.css`); sem PDF no servidor
- **Declaração de matrícula** `Pages/Matriculas/Declaracao/{id}` (área Matrículas; link "Declaração" na lista, só matrícula Ativa; outra situação mostra aviso). **Histórico escolar** `Pages/Alunos/Historico/{alunoId}` (área Alunos; link na lista de Alunos): todos os anos, média final/frequência/situação por disciplina e resultado do ano. **Boletim**: botão Imprimir em `Boletim` (aluno/responsável) e `Desempenho/Aluno` (funcionário), com cabeçalho só na impressão
- `ResultadoAlunoDb.CarregarAsync` (em `Data/ResultadoAluno.cs`) carrega o resultado de uma matrícula enturmada (Desempenho e Histórico)
- **Identificação da escola**: nome, endereço e telefone em `Pages/Configuracoes` (seção "Escola"), colunas `Configuracoes.NomeEscola/Endereco/Telefone` (migration `DadosDaEscola`) e estático `Escola.*` (`Data/Aprovacao.cs`, carregado na partida, como as outras configurações). Aparece no topo, no título da aba, no recibo, declaração, histórico e boletim impresso. Mudanças vão para o registro de ações
- Falta: logo configurável pela tela (continua `wwwroot/img/logo.*`), CNPJ, número de série/autenticidade, assinatura digital, declaração de frequência, impressão do histórico por série completa com carga horária

### Dados de demonstração (para prints e vídeos)
- `dotnet run -- --demo` (só Development) cria 30 alunos fictícios (27 com matrícula Ativa em 4 turmas do ano atual: 5º A, 5º B, 8º A, 1ª série A; 3 Pendentes do ano seguinte), 6 professores (`<nome>.demo@demo.test`, senha de dev), notas dos 4 bimestres, recuperação, faltas, aulas dadas, taxa + 12 mensalidades (a maioria em dia, ~1/3 com atraso) e eventos no calendário. Código em `Data/DadosDemo.cs`
- `dotnet run -- --demo-limpar` remove só o que o `--demo` criou (alunos com `Observacoes = "[demo]"`, professores `@demo.test`, eventos com descrição `[demo]`, turmas que ficaram vazias). Não mexe em turma que já existia
- A primeira família usa `responsavel@escola.test` e o primeiro aluno `aluno@escola.test`, para os usuários de dev verem boletim e mensalidades
- **Esses dados FICAM no banco de dev de propósito**: no "fim do dia" apagar só os dados de teste manuais e os usuários `@escola.test`, NÃO rodar `--demo-limpar` a menos que o usuário peça. Cobranças dependem da data de hoje: para atualizar, rodar `--demo-limpar` e `--demo`

### Limites e pendências (conferir depois)
Lista viva: acrescentar aqui todo limite novo que aparecer e riscar o que for resolvido.
- Dados: aluno órfão "Alberto da Silva" (sem matrícula) no banco de dev compartilhado: matricular de novo pelo CPF ou apagar
- Acesso: sem "esqueci minha senha" por e-mail (só o funcionário gera outra provisória); limite de tentativas de login fica em memória (reiniciar zera)
- Configurações (aprovação, frequência, multa e juros) ficam em propriedades estáticas: serve a uma instância só
- Notas e frequência: sem abono/justificativa de faltas, sem chamada diária (só total de faltas por bimestre), sem regra de recuperação por bimestre
- Desempenho (funcionário): sem exportação/impressão; professor só vê o que lança
- Calendário: sem eventos por turma/série, sem ligar provas às turmas, sem início/fim dos bimestres
- Funcionários: sem permissão de só leitura dentro de uma área; cargos fixos no código (não há cadastro de cargos); lista de sessões revogadas fica em memória; log de ações só cobre as operações listadas em "Registro de ações"
- Financeiro: baixa manual (sem boleto/Pix real), sem descontos/bolsas, sem integração de pagamento online; recibo só no acesso do funcionário (responsável não emite o próprio); logo só por arquivo; sem CNPJ nos documentos
- Dados de demonstração: cobranças dependem da data de hoje (refazer com `--demo-limpar` e `--demo`)
- Testes: a matriz de permissões está em `PermissaoTests.Matriz`: página nova = acrescentar a linha lá. Não há testes do conteúdo/fluxo das telas (só quem pode abrir)
- Publicação: logo atual provisória (fundo branco no tema escuro); prints com a barra do navegador; e-mail pessoal visível no histórico de commits (avaliar noreply do GitHub)
- Sala de aula: sem editar publicação (só excluir e refazer), sem anexos/upload, sem entrega de atividade pelo aluno, sem comentários, sem aviso de nova publicação, sem horário/grade semanal; o aluno vê só a turma da matrícula mais recente; mural limitado às 100 publicações mais novas

## Cuidados
- `psql` instalado. Para rodar SQL com a conexão do user-secrets sem expor a senha: ler `dotnet user-secrets list` e passar Host/Port/Username/Database por parâmetro e a senha por `PGPASSWORD`
- Rodar servidores de teste em background: guardar o PID e encerrar com `kill <pid>`. Não usar `pkill -f` com o nome da DLL, pois mata o próprio shell
- Dados de teste criados no banco compartilhado devem ser apagados ao final (exceto os de demonstração, ver acima)
- Fim do dia: quando o usuário rodar `git commit` e `git push` dizendo que finalizamos por hoje, apagar do banco os dados de exemplo (matrículas de teste e usuários `@escola.test`). Em Development os usuários de teste são recriados na próxima execução
- Cada perfil só vê na tela inicial os cartões do que lhe cabe. Página fora do perfil: Acesso negado
- Matar o servidor de teste: `dotnet run` deixa um processo filho; se `kill $(cat pid)` não liberar a porta, ache o PID real com `ss -ltnp | grep 5006`
