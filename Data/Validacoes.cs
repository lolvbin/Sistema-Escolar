using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SistemaEscolar.Data;

public static class Texto
{
    public static string Digitos(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());

    public static string FormatarCpf(string? s)
    {
        var d = Digitos(s);
        return d.Length == 11 ? $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}" : s ?? "";
    }

    public static string FormatarTelefone(string? s)
    {
        var d = Digitos(s);
        return d.Length == 11 ? $"({d[..2]}){d[2..7]}-{d[7..]}" : s ?? "";
    }
}

/// <summary>Só letras, espaços, apóstrofo e hífen. Nenhum número.</summary>
public class NomeAttribute : ValidationAttribute
{
    private static readonly Regex Rx = new(@"^[\p{L}][\p{L}\s'\-]*$", RegexOptions.Compiled);

    public NomeAttribute() => ErrorMessage = "Use apenas letras (sem números ou símbolos)";

    public override bool IsValid(object? value) =>
        value is not string s || s.Length == 0 || Rx.IsMatch(s.Trim());
}

/// <summary>CPF com 11 dígitos e dígitos verificadores corretos.</summary>
public class CpfAttribute : ValidationAttribute
{
    public CpfAttribute() => ErrorMessage = "CPF inválido (11 números)";

    public override bool IsValid(object? value)
    {
        if (value is not string s || s.Length == 0) return true; // [Required] cuida do vazio
        if (s.Any(c => !(char.IsDigit(c) || c == '.' || c == '-'))) return false;

        var d = Texto.Digitos(s);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;

        for (int t = 9; t <= 10; t++)
        {
            int soma = 0;
            for (int i = 0; i < t; i++) soma += (d[i] - '0') * (t + 1 - i);
            int dig = soma * 10 % 11 % 10;
            if (dig != d[t] - '0') return false;
        }
        return true;
    }
}

/// <summary>Celular brasileiro: (31)9xxxx-xxxx.</summary>
public class TelefoneAttribute : ValidationAttribute
{
    public TelefoneAttribute() => ErrorMessage = "Telefone inválido. Use (31)9xxxx-xxxx";

    public override bool IsValid(object? value)
    {
        if (value is not string s || s.Length == 0) return true;
        if (s.Any(c => !(char.IsDigit(c) || c is '(' or ')' or '-' or ' '))) return false;
        var d = Texto.Digitos(s);
        return d.Length == 11 && d[2] == '9';
    }
}
