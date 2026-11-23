# 🎓 Sistema Escolar

Sistema web para gestão escolar: matrícula, alunos, professores, turmas e notas, com acesso separado para cada perfil.
Feito para ser **leve**: HTML renderizado no servidor, quase nenhum JavaScript e CSS enxuto, para rodar bem até em computadores antigos.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Razor_Pages-512BD4)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-EF_Core-336791?logo=postgresql&logoColor=white)
![Modo escuro](https://img.shields.io/badge/tema-claro_%2F_escuro-222)

## ✨ Módulos

| Módulo | O que faz | Quem usa |
|---|---|---|
| **Matrícula** | Cria o aluno junto com a matrícula (não existe aluno sem matrícula), rematrícula pelo CPF, status Pendente / Ativa / Cancelada / Transferida | Funcionário |
| **Alunos** | Consulta, correção de dados e criação de acessos (aluno e responsável) | Funcionário |
| **Professores** | Cadastro, login com senha provisória, inativação | Funcionário |
| **Disciplinas** | Catálogo por série, com disciplinas padrão | Funcionário |
| **Turmas** | Série + ano + letra, enturmação de alunos ativos e professor por disciplina | Funcionário |
| **Notas e frequência** | Notas por bimestre, recuperação, aulas dadas e faltas | Professor |
| **Configurações** | Média de aprovação, de recuperação e frequência mínima | Funcionário |
| **Financeiro** | Contrato, taxa e mensalidades, baixa de pagamentos; libera a matrícula pendente | Funcionário (responsável consulta) |
| **Calendário** | Grade mensal com feriados, recessos, provas e reuniões | Todos veem, funcionário cadastra |
| **Boletim** | Notas, recuperação, frequência e situação (aprovado, recuperação, reprovado) | Aluno e responsável |

**Em breve:** sala de aula, multa e juros, recibos e relatório de inadimplência.

## 👥 Perfis de acesso

Responsável, Funcionário, Professor e Aluno. Cada perfil vê só o menu e as telas que lhe cabem; o resto responde "Acesso negado".

## 🧱 Tecnologias

- C# / ASP.NET Core (.NET 10), Razor Pages
- Entity Framework Core + PostgreSQL (Npgsql 10.x)
- Autenticação por cookie, senhas com PBKDF2
- Tema claro/escuro por variáveis CSS, sem Bootstrap, jQuery ou frameworks de JS
- Validação feita no servidor; máscaras de CPF, telefone e data em um único `site.js`

## 🚀 Como rodar

Requisitos: .NET 10 SDK e um PostgreSQL acessível.

```bash
# 1. Guardar a conexão (não é versionada)
dotnet user-secrets set "ConnectionStrings:Escola" "Host=localhost;Port=5432;Database=escola;Username=usuario;Password=senha"

# 2. Rodar
dotnet run
```

Abra <http://localhost:5006>. As tabelas são criadas na primeira execução.

Em **Development** são criados 4 usuários de teste (`responsavel@`, `funcionario@`, `professor@` e `aluno@escola.test`). Em produção eles não existem: defina a conexão pela variável `ConnectionStrings__Escola` e crie os usuários reais por outro meio.

## 📁 Estrutura

```
Data/            Entidades, validações e criação das tabelas
Pages/           Telas por módulo (Matriculas, Alunos, Professores, Disciplinas, Turmas, Notas, Boletim)
Pages/Shared/    Layout e partials compartilhados
wwwroot/         site.css e site.js
```

## 📝 Observações

- O banco é criado com `EnsureCreated` e SQL simples (sem migrations). Antes de ter dados reais, vale adotar migrations.
- Projeto em desenvolvimento, feito em passos pequenos, um módulo por vez. O contexto detalhado está em [`CLAUDE.md`](CLAUDE.md).
