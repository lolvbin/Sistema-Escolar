using System.Text;
using System.Text.RegularExpressions;

namespace SistemaEscolar.Data;

/// <summary>Logo enviada pela escola (linha única Id=1). Fica no banco: não depende de pasta do servidor e vai junto no backup.</summary>
public class LogoEscola
{
    public int Id { get; set; } = 1;
    public byte[] Dados { get; set; } = Array.Empty<byte>();
    public string Tipo { get; set; } = "image/png";
    public DateTime AtualizadoEm { get; set; }  // UTC
}

public static class Logos
{
    public const int MaxBytes = 512 * 1024;

    /// <summary>Confere tamanho e conteúdo (PNG, JPEG ou SVG sem script). Devolve o tipo, ou null e a mensagem de erro.</summary>
    public static string? Validar(byte[] b, out string? erro)
    {
        erro = null;
        if (b.Length == 0) { erro = "Escolha um arquivo"; return null; }
        if (b.Length > MaxBytes) { erro = "Arquivo grande demais (máximo 512 KB)"; return null; }
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        var t = Encoding.UTF8.GetString(b);
        if (Regex.IsMatch(t[..Math.Min(t.Length, 2048)], "<svg[\\s>]", RegexOptions.IgnoreCase))
        {
            if (Regex.IsMatch(t, "<script|javascript:|<foreignObject|\\son\\w+\\s*=|<iframe|<embed|<object", RegexOptions.IgnoreCase))
            { erro = "SVG com script ou conteúdo ativo não é aceito"; return null; }
            return "image/svg+xml";
        }
        erro = "Use uma imagem PNG, JPG ou SVG";
        return null;
    }
}
