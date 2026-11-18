using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(o =>
{
    // Matrículas: só funcionário
    o.Conventions.AuthorizeFolder("/Matriculas", "Funcionario");
    // Sala de aula: professor e aluno
    o.Conventions.AuthorizePage("/SalaDeAula", "SalaDeAula");
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
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("Funcionario", p => p.RequireRole(nameof(Perfil.Funcionario)))
    .AddPolicy("SalaDeAula", p => p.RequireRole(nameof(Perfil.Professor), nameof(Perfil.Aluno)));
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

// Início: cria o banco se não existir (sem migrations por enquanto).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EscolaContext>();
    db.Database.EnsureCreated();
    UsuarioSeed.Preparar(db, app.Environment.IsDevelopment());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous(); // css/js precisam carregar na tela de login
app.MapRazorPages().WithStaticAssets();

app.Run();
