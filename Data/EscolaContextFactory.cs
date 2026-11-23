using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SistemaEscolar.Data;

// Usado só pelas ferramentas do EF (dotnet ef migrations ...): evita subir a aplicação inteira.
// Lê a conexão do user-secrets ou da variável de ambiente ConnectionStrings__Escola.
public class EscolaContextFactory : IDesignTimeDbContextFactory<EscolaContext>
{
    public EscolaContext CreateDbContext(string[] args)
    {
        var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddUserSecrets(typeof(EscolaContext).Assembly)
            .AddEnvironmentVariables()
            .Build();
        var cs = cfg.GetConnectionString("Escola")
            ?? "Host=localhost;Port=5432;Database=escola;Username=escola;Password=escola_dev";
        return new EscolaContext(new DbContextOptionsBuilder<EscolaContext>().UseNpgsql(cs).Options);
    }
}
