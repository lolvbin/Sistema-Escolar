using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

public enum Perfil { Responsavel, Funcionario, Professor, Aluno }

public static class PerfilExt
{
    public static string Nome(this Perfil p) => p switch
    {
        Perfil.Responsavel => "Responsável",
        Perfil.Funcionario => "Funcionário",
        Perfil.Professor => "Professor",
        _ => "Aluno"
    };
}

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";   // sempre minúsculo
    public string SenhaHash { get; set; } = "";
    public Perfil Perfil { get; set; }
    public bool Ativo { get; set; } = true;
    public bool DeveTrocarSenha { get; set; }  // senha provisória: obriga a trocar no primeiro acesso
}

// Hash de senha com PBKDF2 (nativo do .NET, sem bibliotecas extras). Formato: iterações.salt.hash (base64)
public static class Senha
{
    const int Iteracoes = 100_000;

    public static string Gerar(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iteracoes, HashAlgorithmName.SHA256, 32);
        return $"{Iteracoes}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Conferir(string senha, string armazenado)
    {
        var p = armazenado.Split('.');
        if (p.Length != 3 || !int.TryParse(p[0], out var it)) return false;
        var salt = Convert.FromBase64String(p[1]);
        var esperado = Convert.FromBase64String(p[2]);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, it, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(hash, esperado);
    }

    // Senha provisória legível (sem 0/O/1/l/I) para o primeiro acesso
    public static string Provisoria() =>
        RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789", 10);

    // Hash falso, usado quando o e-mail não existe (mantém o tempo de resposta parecido)
    public static readonly string Falso = Gerar("nao-existe");
}

public static class UsuarioSeed
{
    // SOMENTE para desenvolvimento. Troque/remova antes de ir para produção.
    const string SenhaDev = "Escola@123";

    // Cria as contas de teste (só em Development). As tabelas vêm das migrations.
    public static void Semear(EscolaContext db, bool desenvolvimento)
    {
        if (!desenvolvimento) return;

        // Cria só as contas de teste que ainda não existem
        var existentes = db.Usuarios.Select(u => u.Email).ToHashSet();
        var testes = new[]
        {
            Novo("Responsável Teste", "responsavel@escola.test", Perfil.Responsavel),
            Novo("Funcionário Teste", "funcionario@escola.test", Perfil.Funcionario),
            Novo("Professor Teste", "professor@escola.test", Perfil.Professor),
            Novo("Aluno Teste", "aluno@escola.test", Perfil.Aluno),
        };
        db.Usuarios.AddRange(testes.Where(t => !existentes.Contains(t.Email)));
        db.SaveChanges();
    }

    static Usuario Novo(string nome, string email, Perfil perfil) =>
        new() { Nome = nome, Email = email, Perfil = perfil, SenhaHash = Senha.Gerar(SenhaDev) };
}
