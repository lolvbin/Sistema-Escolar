using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

// Teste de integração: aplica as migrations num banco novo (criado e apagado pelo teste) e usa o modelo de verdade.
// Precisa de um PostgreSQL. Informe a conexão (qualquer banco do servidor, com permissão de criar bancos):
//   ESCOLA_TEST_DB="Host=localhost;Port=5432;Database=postgres;Username=escola;Password=escola_dev" dotnet test
// Sem a variável, os testes abaixo são ignorados.
public sealed class FatoComBancoAttribute : FactAttribute
{
    public FatoComBancoAttribute()
    {
        if (Environment.GetEnvironmentVariable("ESCOLA_TEST_DB") == null)
            Skip = "Defina ESCOLA_TEST_DB para rodar os testes com PostgreSQL";
    }
}

public class BancoTests : IDisposable
{
    readonly string? _admin = Environment.GetEnvironmentVariable("ESCOLA_TEST_DB");
    readonly string _nome = "escola_teste_" + Guid.NewGuid().ToString("N")[..10];

    EscolaContext Novo()
    {
        var b = new NpgsqlConnectionStringBuilder(_admin) { Database = _nome };
        return new EscolaContext(new DbContextOptionsBuilder<EscolaContext>().UseNpgsql(b.ConnectionString).Options);
    }

    void CriarBanco()
    {
        using var c = new NpgsqlConnection(_admin);
        c.Open();
        using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_nome}\"", c);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_admin == null) return;
        NpgsqlConnection.ClearAllPools();
        using var c = new NpgsqlConnection(_admin);
        c.Open();
        using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_nome}\" WITH (FORCE)", c);
        cmd.ExecuteNonQuery();
    }

    [FatoComBanco]
    public void Migrations_criam_o_banco_do_zero_com_a_configuracao_padrao()
    {
        CriarBanco();
        using var db = Novo();
        db.Database.Migrate();

        Assert.Empty(db.Database.GetPendingMigrations());
        var c = db.Configuracoes.Single();
        Assert.Equal(6.0m, c.MediaMinima);
        Assert.Equal(4.0m, c.MediaRecuperacao);
        Assert.Equal(75.0m, c.FrequenciaMinima);
        Assert.Equal(2.0m, c.MultaPercentual);
        Assert.Equal(1.0m, c.JurosMensal);
    }

    [FatoComBanco]
    public void Fluxo_matricula_contrato_e_taxa_libera_a_matricula_e_grava_no_banco()
    {
        CriarBanco();
        using (var db = Novo())
        {
            db.Database.Migrate();
            var aluno = new Aluno
            {
                Nome = "Aluno Teste", DataNascimento = new DateTime(2016, 1, 1), Cpf = "111.444.777-35", MesmoResponsavel = true,
                Academico = new Responsavel { Nome = "Resp", Cpf = "529.982.247-25", Telefone = "(31)91234-5678", Email = "r@x.com" },
                Financeiro = new Responsavel { Nome = "Resp", Cpf = "529.982.247-25", Telefone = "(31)91234-5678", Email = "r@x.com" }
            };
            var m = new Matricula { Aluno = aluno, Serie = Serie.Fund5, Turno = Turno.Manha, AnoLetivo = 2027, ContratoAssinadoEm = DateTime.Today };
            m.Aluno = aluno;
            db.Matriculas.Add(m);
            db.SaveChanges();
            db.Cobrancas.Add(new Cobranca { MatriculaId = m.Id, Tipo = TipoCobranca.Matricula, Numero = 0, Descricao = "Taxa", Vencimento = DateTime.Today, Valor = 300m, PagoEm = DateTime.Today, Forma = FormaPagamento.Pix, ValorPago = 300m });
            db.SaveChanges();
        }

        using (var db = Novo())
        {
            var m = db.Matriculas.Single();
            Assert.Equal(StatusMatricula.Pendente, m.Status);   // toda matrícula nasce Pendente
            Assert.True(RegrasFinanceiro.TentarLiberar(m, db.Cobrancas.ToList()));
            db.SaveChanges();
        }

        using (var db = Novo())
            Assert.Equal(StatusMatricula.Ativa, db.Matriculas.Single().Status);
    }

    [FatoComBanco]
    public void Banco_impede_cpf_duplicado_de_aluno_e_matricula_repetida_no_ano()
    {
        CriarBanco();
        using var db = Novo();
        db.Database.Migrate();
        Aluno NovoAluno(string cpf) => new()
        {
            Nome = "Aluno", DataNascimento = new DateTime(2016, 1, 1), Cpf = cpf,
            Academico = new Responsavel { Nome = "R", Cpf = "529.982.247-25", Telefone = "(31)91234-5678", Email = "r@x.com" },
            Financeiro = new Responsavel { Nome = "R", Cpf = "529.982.247-25", Telefone = "(31)91234-5678", Email = "r@x.com" }
        };
        var a = NovoAluno("111.444.777-35");
        db.Alunos.Add(a);
        db.SaveChanges();

        db.Alunos.Add(NovoAluno("111.444.777-35"));
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();

        db.Matriculas.Add(new Matricula { AlunoId = a.Id, Serie = Serie.Fund5, Turno = Turno.Manha, AnoLetivo = 2027 });
        db.SaveChanges();
        db.Matriculas.Add(new Matricula { AlunoId = a.Id, Serie = Serie.Fund6, Turno = Turno.Tarde, AnoLetivo = 2027 });
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
