using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
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
    scope.ServiceProvider.GetRequiredService<EscolaContext>().Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
