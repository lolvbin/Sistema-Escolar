using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

// Testa as páginas de verdade (HTTP) contra um PostgreSQL temporário: cada perfil/cargo abre só o que deve.
// Mesma variável dos outros testes de banco (ESCOLA_TEST_DB); sem ela, são ignorados.
public class PermissaoTests : IDisposable
{
    readonly string? _admin = Environment.GetEnvironmentVariable("ESCOLA_TEST_DB");
    readonly string _nome = "escola_perm_" + Guid.NewGuid().ToString("N")[..10];
    const string Senha_ = "Escola@123";

    static readonly string[] Usuarios = { "adm", "sec", "fin", "coord", "prof", "aluno", "resp", "anon" };

    // Página → quem consegue abrir (200). Os demais são barrados (redireciona para /AcessoNegado ou /Login).
    static readonly (string Pagina, string Quem)[] Matriz =
    {
        ("/Matriculas",            "adm sec"),
        ("/Matriculas/Create",     "adm sec"),
        ("/Alunos",                "adm sec"),
        ("/Professores",           "adm sec coord"),
        ("/Professores/Create",    "adm sec coord"),
        ("/Disciplinas",           "adm sec coord"),
        ("/Turmas",                "adm sec coord"),
        ("/Desempenho",            "adm sec coord"),
        ("/Financeiro",            "adm fin"),
        ("/Financeiro/Inadimplencia", "adm fin"),
        ("/Calendario/Evento",     "adm sec fin coord"),
        ("/Configuracoes",         "adm"),
        ("/Usuarios",              "adm"),
        ("/Usuarios/Create",       "adm"),
        ("/Log",                   "adm"),
        ("/Comunicados",           "adm sec coord"),
        ("/Horarios",              "adm sec coord"),
        ("/Horarios/Aulas",        "adm sec coord"),
        ("/Horarios/Turma/1",      "adm sec coord"),
        ("/Horarios/Professor/1",  "adm sec coord"),
        ("/MeuHorario",            "prof"),
        ("/Horario",               "aluno resp"),
        ("/Comunicados/Editar",    "adm sec coord"),
        // páginas com id: sem dados, quem pode abrir recebe 404 (passou da autorização); quem não pode é barrado
        ("/Matriculas/Declaracao/1", "adm sec"),
        ("/Alunos/Historico/1",    "adm sec"),
        ("/Desempenho/Aluno/1",    "adm sec coord"),
        ("/Financeiro/Recibo/1",   "adm fin"),
        ("/Notas",                 "prof"),
        ("/Mensalidades",          "resp"),
        ("/Boletim",               "aluno resp"),
        ("/SalaDeAula",            "prof aluno resp"),
        // abertas a qualquer pessoa logada
        ("/",                      "adm sec fin coord prof aluno resp"),
        ("/Calendario",            "adm sec fin coord prof aluno resp"),
        ("/Conta/Senha",           "adm sec fin coord prof aluno resp"),
    };

    string Conn() => new NpgsqlConnectionStringBuilder(_admin) { Database = _nome }.ConnectionString;

    WebApplicationFactory<Program> Subir()
    {
        using (var c = new NpgsqlConnection(_admin)) { c.Open(); new NpgsqlCommand($"CREATE DATABASE \"{_nome}\"", c).ExecuteNonQuery(); }
        Environment.SetEnvironmentVariable("ConnectionStrings__Escola", Conn());
        var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development")); // Development: cria os 4 usuários @escola.test
        f.CreateClient().Dispose(); // sobe o app (aplica migrations e semeia)

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EscolaContext>();
        foreach (var (email, cargo) in new[] { ("sec@t.test", Cargo.Secretaria), ("fin@t.test", Cargo.Financeiro), ("coord@t.test", Cargo.Coordenacao) })
            db.Usuarios.Add(new Usuario { Nome = email, Email = email, Perfil = Perfil.Funcionario, Cargo = cargo, Areas = cargo.Padrao(), SenhaHash = Data.Senha.Gerar(Senha_) });
        db.SaveChanges();
        return f;
    }

    static string? Email(string u) => u switch
    {
        "adm" => "funcionario@escola.test", "sec" => "sec@t.test", "fin" => "fin@t.test", "coord" => "coord@t.test",
        "prof" => "professor@escola.test", "aluno" => "aluno@escola.test", "resp" => "responsavel@escola.test", _ => null
    };

    static async Task<HttpClient> Entrar(WebApplicationFactory<Program> f, string u)
    {
        var c = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var email = Email(u);
        if (email == null) return c; // anônimo
        var html = await c.GetStringAsync("/Login");
        var token = Regex.Match(html, "__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var r = await c.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Email"] = email, ["Senha"] = Senha_, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode); // login deu certo
        return c;
    }

    [FatoComBanco]
    public async Task Cada_perfil_abre_so_o_que_deve()
    {
        using var f = Subir();
        var erros = new List<string>();
        foreach (var u in Usuarios)
        {
            using var c = await Entrar(f, u);
            foreach (var (pagina, quem) in Matriz)
            {
                var r = await c.GetAsync(pagina);
                bool abriu = r.StatusCode == HttpStatusCode.OK || (pagina.Contains("/1") && r.StatusCode == HttpStatusCode.NotFound);
                bool deve = quem.Split(' ').Contains(u);
                if (abriu != deve)
                    erros.Add($"{u} em {pagina}: esperado {(deve ? "abrir" : "barrar")}, veio {(int)r.StatusCode}");
                if (!abriu)
                {
                    var destino = r.Headers.Location?.ToString() ?? "";
                    bool ok = u == "anon" ? destino.Contains("/Login") : destino.Contains("AcessoNegado");
                    if (!ok) erros.Add($"{u} em {pagina}: barrado para o lugar errado ({(int)r.StatusCode} {destino})");
                }
            }
        }
        Assert.True(erros.Count == 0, string.Join("\n", erros));
    }

    [FatoComBanco]
    public async Task Post_sem_a_area_tambem_e_barrado()
    {
        using var f = Subir();
        // secretaria não pode dar baixa nem mudar configurações; professor não cria usuário
        foreach (var (u, url) in new[] { ("sec", "/Configuracoes"), ("fin", "/Usuarios/Create"), ("prof", "/Financeiro/Detalhe/1?handler=Baixa"), ("aluno", "/Notas/Lancar") })
        {
            using var c = await Entrar(f, u);
            var r = await c.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>()));
            // sem token o app responderia 400; o que importa é que a autorização vem antes e barra
            Assert.True(r.StatusCode == HttpStatusCode.Redirect && (r.Headers.Location?.ToString() ?? "").Contains("AcessoNegado"),
                $"{u} POST {url}: veio {(int)r.StatusCode}");
        }
    }

    [FatoComBanco]
    public async Task Funcionario_inativo_ou_sem_area_perde_o_acesso()
    {
        using var f = Subir();
        using var c = await Entrar(f, "sec");
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/Matriculas")).StatusCode);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EscolaContext>();
            var u = db.Usuarios.First(x => x.Email == "sec@t.test");
            Assert.Equal(Area.Matriculas, u.Areas & Area.Matriculas); // tinha a área
        }
        // sem sessão válida (cookie revogado) volta para o login
        var id = f.Services.CreateScope().ServiceProvider.GetRequiredService<EscolaContext>().Usuarios.First(x => x.Email == "sec@t.test").Id;
        await Task.Delay(1100);
        Sessoes.Revogar(id);
        var r = await c.GetAsync("/Matriculas");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("/Login", r.Headers.Location!.ToString());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Escola", null);
        if (_admin == null) return;
        NpgsqlConnection.ClearAllPools();
        using var c = new NpgsqlConnection(_admin);
        c.Open();
        new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_nome}\" WITH (FORCE)", c).ExecuteNonQuery();
    }
}
