using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(o =>
{
    // Funcionário: cada área tem a sua política (cargo = conjunto de áreas, ver Data/Areas.cs)
    o.Conventions.AuthorizeFolder("/Matriculas", "Area:Matriculas");
    o.Conventions.AuthorizeFolder("/Alunos", "Area:Alunos");
    o.Conventions.AuthorizeFolder("/Professores", "Area:Professores");
    o.Conventions.AuthorizeFolder("/Disciplinas", "Area:Disciplinas");
    o.Conventions.AuthorizeFolder("/Turmas", "Area:Turmas");
    o.Conventions.AuthorizeFolder("/Desempenho", "Area:Desempenho");
    o.Conventions.AuthorizeFolder("/Financeiro", "Area:Financeiro");
    o.Conventions.AuthorizePage("/Calendario/Evento", "Area:Calendario"); // todos veem o calendário; só quem tem a área cadastra
    o.Conventions.AuthorizePage("/Configuracoes", "Area:Configuracoes");
    o.Conventions.AuthorizeFolder("/Usuarios", "Area:Usuarios");
    o.Conventions.AllowAnonymousToPage("/PrimeiroAcesso");
    o.Conventions.AuthorizePage("/Mensalidades", "Responsavel");
    // Notas: lançamento só pelo professor
    o.Conventions.AuthorizeFolder("/Notas", "Professor");
    // Boletim: aluno e responsável
    o.Conventions.AuthorizePage("/Boletim", "Boletim");
    // Sala de aula: professor publica; aluno e responsável leem
    o.Conventions.AuthorizeFolder("/SalaDeAula", "SalaDeAula");
    o.Conventions.AuthorizePage("/SalaDeAula/Turma", "Professor");
});

// Login por cookie. Tudo exige login, exceto o que tiver [AllowAnonymous] (Login, Prévia, Erro).
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.AccessDeniedPath = "/AcessoNegado";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.Name = "escola.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        // Usuário inativado ou com acesso alterado perde a sessão na hora
        o.Events.OnValidatePrincipal = async ctx =>
        {
            if (Sessoes.Invalida(ctx.Principal, ctx.Properties.IssuedUtc))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync();
            }
        };
    });
var auth = builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("Responsavel", p => p.RequireRole(nameof(Perfil.Responsavel)))
    .AddPolicy("Professor", p => p.RequireRole(nameof(Perfil.Professor)))
    .AddPolicy("Boletim", p => p.RequireRole(nameof(Perfil.Aluno), nameof(Perfil.Responsavel)))
    .AddPolicy("SalaDeAula", p => p.RequireRole(nameof(Perfil.Professor), nameof(Perfil.Aluno), nameof(Perfil.Responsavel)));
foreach (var area in AreaExt.Individuais)
    auth.AddPolicy("Area:" + area, p => p.RequireRole(nameof(Perfil.Funcionario)).RequireAssertion(c => c.User.TemArea(area)));
// Connection string vem do user-secrets (dev) ou de variável de ambiente (ConnectionStrings__Escola).
var cs = builder.Configuration.GetConnectionString("Escola")
    ?? throw new InvalidOperationException("Defina ConnectionStrings:Escola (user-secrets).");
builder.Services.AddDbContext<EscolaContext>(o => o.UseNpgsql(cs));

var app = builder.Build();

// Datas e números no padrão brasileiro (dd/MM/yyyy)
var pt = new System.Globalization.CultureInfo("pt-BR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(pt),
    SupportedCultures = new[] { pt },
    SupportedUICultures = new[] { pt }
});

// Início: aplica as migrations (cria/atualiza o banco), cria os usuários de teste em Development
// e carrega as configurações de aprovação/financeiro para a memória.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EscolaContext>();
    db.Database.Migrate();
    UsuarioSeed.Semear(db, app.Environment.IsDevelopment());
    TabelaConfiguracoes.Carregar(db);

    // Dados fictícios para prints e vídeos (só em Development): dotnet run -- --demo | --demo-limpar
    if (args.Contains("--demo") || args.Contains("--demo-limpar"))
    {
        Console.WriteLine(app.Environment.IsDevelopment()
            ? DadosDemo.Executar(db, args.Contains("--demo-limpar"))
            : "Os dados de demonstração só podem ser criados em Development.");
        return;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Senha provisória: enquanto não trocar, só pode abrir a tela de troca (e sair)
app.Use(async (ctx, next) =>
{
    if (ctx.User.HasClaim(Autenticacao.ClaimTrocarSenha, "1"))
    {
        var p = ctx.Request.Path;
        if (!p.StartsWithSegments("/Conta/Senha") && !p.StartsWithSegments("/Logout") && !(p.Value ?? "").Contains('.'))
        {
            ctx.Response.Redirect("/Conta/Senha");
            return;
        }
    }
    await next();
});

app.MapStaticAssets().AllowAnonymous(); // css/js precisam carregar na tela de login
app.MapRazorPages().WithStaticAssets();

app.Run();
