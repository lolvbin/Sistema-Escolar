using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace SistemaEscolar.Data;

// Limite de tentativas de login/troca de senha. Fica em memória (app de uma instância só):
// reiniciar o servidor zera os contadores. Chaves: e-mail (limite baixo) e IP (limite alto, por causa de NAT).
public static class LimiteLogin
{
    public const int MaxPorEmail = 5;
    public const int MaxPorIp = 30;
    static readonly TimeSpan Janela = TimeSpan.FromMinutes(15);
    static readonly ConcurrentDictionary<string, (int N, DateTime Ate)> Falhas = new();

    /// <summary>Tempo restante de bloqueio, ou null se pode tentar.</summary>
    public static TimeSpan? Bloqueado(string chave, int max)
    {
        if (Falhas.TryGetValue(chave, out var f) && f.N >= max && f.Ate > DateTime.UtcNow)
            return f.Ate - DateTime.UtcNow;
        return null;
    }

    public static void Falhou(string chave)
    {
        var agora = DateTime.UtcNow;
        Falhas.AddOrUpdate(chave,
            _ => (1, agora + Janela),
            (_, f) => f.Ate <= agora ? (1, agora + Janela) : (f.N + 1, agora + Janela));
        if (Falhas.Count > 10_000)
            foreach (var k in Falhas.Where(x => x.Value.Ate <= agora).Select(x => x.Key).ToList()) Falhas.TryRemove(k, out _);
    }

    public static void Limpar(string chave) => Falhas.TryRemove(chave, out _);

    public static string Minutos(TimeSpan t)
    {
        var m = (int)Math.Ceiling(t.TotalMinutes);
        return m <= 1 ? "1 minuto" : $"{m} minutos";
    }
}

public static class Autenticacao
{
    public const string ClaimTrocarSenha = "trocar-senha";

    public static ClaimsPrincipal CriarPrincipal(Usuario u)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Nome),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, u.Perfil.ToString()),
        };
        if (u.DeveTrocarSenha) claims.Add(new Claim(ClaimTrocarSenha, "1"));
        if (u.Perfil == Perfil.Funcionario) claims.Add(new Claim(AreaExt.ClaimAreas, ((int)u.Areas).ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>Senha nova: 8+ caracteres, com letra e número.</summary>
    public static string? ValidarSenhaNova(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length < 8) return "A senha deve ter pelo menos 8 caracteres";
        if (!s.Any(char.IsLetter) || !s.Any(char.IsDigit)) return "Use letras e números na senha";
        return null;
    }
}

// Sessões derrubadas: ao inativar um usuário, mudar suas áreas ou gerar nova senha, os cookies emitidos antes
// deixam de valer na próxima requisição. Fica em memória (uma instância só): reiniciar o servidor zera a lista.
public static class Sessoes
{
    static readonly ConcurrentDictionary<int, DateTimeOffset> Revogadas = new();

    // O IssuedUtc do cookie tem precisão de segundos: guardamos o instante também em segundos, e só rejeita
    // cookie emitido ANTES desse segundo (quem entra de novo logo depois da revogação não é derrubado).
    public static void Revogar(int usuarioId) =>
        Revogadas[usuarioId] = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public static bool Invalida(ClaimsPrincipal? user, DateTimeOffset? emitidoEm)
    {
        if (!int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        return Revogadas.TryGetValue(id, out var quando) && (emitidoEm == null || emitidoEm.Value < quando);
    }
}
