using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

public enum TipoEvento
{
    [Display(Name = "Evento")] Evento,
    [Display(Name = "Feriado")] Feriado,
    [Display(Name = "Recesso")] Recesso,
    [Display(Name = "Prova")] Prova,
    [Display(Name = "Reunião")] Reuniao
}

// Evento do calendário acadêmico (vale para a escola toda). Dura um dia ou vários (Inicio..Fim).
public class Evento
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o título")]
    [StringLength(100)]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = "";

    [Required(ErrorMessage = "Selecione o tipo")]
    public TipoEvento? Tipo { get; set; }

    [Required(ErrorMessage = "Informe a data")]
    [DataType(DataType.Date)]
    [Display(Name = "Data de início")]
    public DateTime? Inicio { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data final (se durar mais de um dia)")]
    public DateTime? Fim { get; set; }

    [StringLength(300)]
    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped, ValidateNever]
    public DateTime UltimoDia => (Fim ?? Inicio ?? DateTime.MinValue).Date;

    // Chaves com prefixo "Evento." porque a tela usa [BindProperty] Evento
    public void Validar(ModelStateDictionary ms)
    {
        if (Inicio is { } i && i.Year < 2000)
            ms.AddModelError("Evento.Inicio", "Data inválida");
        if (Inicio != null && Fim != null && Fim < Inicio)
            ms.AddModelError("Evento.Fim", "A data final não pode ser antes do início");
    }

    public void Normalizar()
    {
        Titulo = Titulo.Trim();
        Descricao = string.IsNullOrWhiteSpace(Descricao) ? null : Descricao.Trim();
        if (Fim == Inicio) Fim = null;
    }
}
