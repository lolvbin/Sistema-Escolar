using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Comunicados;

public class EditarModel : PageModel
{
    private readonly EscolaContext _db;
    public EditarModel(EscolaContext db) => _db = db;

    public class Entrada
    {
        public string? Titulo { get; set; }
        public string? Texto { get; set; }
        public string? Link { get; set; }
        public PublicoComunicado Publico { get; set; }
        public int? TurmaId { get; set; }
        public string? ValidoAte { get; set; }   // dd/MM/yyyy
        public bool Fixado { get; set; }
    }

    [BindProperty] public Entrada Input { get; set; } = new();
    public int? Id { get; set; }
    public List<SelectListItem> Turmas { get; set; } = new();

    private async Task CarregarTurmas()
    {
        var ano = DateTime.Today.Year;
        Turmas = (await _db.Turmas.AsNoTracking().Where(t => t.Ativa && t.AnoLetivo >= ano).OrderBy(t => t.AnoLetivo).ThenBy(t => t.Serie).ThenBy(t => t.Letra).ToListAsync())
            .Select(t => new SelectListItem($"{t.Nome} ({t.AnoLetivo})", t.Id.ToString())).ToList();
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await CarregarTurmas();
        Id = id;
        if (id == null) { Input = new Entrada { ValidoAte = DateTime.Today.AddDays(7).ToString("dd/MM/yyyy") }; return Page(); }
        var c = await _db.Comunicados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        Input = new Entrada { Titulo = c.Titulo, Texto = c.Texto, Link = c.Link, Publico = c.Publico, TurmaId = c.TurmaId, ValidoAte = c.ValidoAte.ToString("dd/MM/yyyy"), Fixado = c.Fixado };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        await CarregarTurmas();
        Id = id;
        Comunicado? c = null;
        if (id != null) { c = await _db.Comunicados.FirstOrDefaultAsync(x => x.Id == id); if (c == null) return NotFound(); }

        var titulo = (Input.Titulo ?? "").Trim();
        var texto = (Input.Texto ?? "").Trim();
        var link = (Input.Link ?? "").Trim();
        if (titulo == "") ModelState.AddModelError("Input.Titulo", "Informe o título");
        else if (titulo.Length > 100) ModelState.AddModelError("Input.Titulo", "Use até 100 caracteres");
        if (texto.Length > 2000) ModelState.AddModelError("Input.Texto", "Use até 2000 caracteres");
        if (texto == "" && link == "") ModelState.AddModelError("Input.Texto", "Escreva o texto ou informe um link");
        if (link != "" && !Publicacao.LinkValido(link))
            ModelState.AddModelError("Input.Link", "Link inválido. Use um endereço começando com http:// ou https://");
        if (!Enum.IsDefined(Input.Publico)) ModelState.AddModelError("Input.Publico", "Selecione o público");
        if (Input.TurmaId != null && Input.Publico == PublicoComunicado.Funcionarios)
            ModelState.AddModelError("Input.TurmaId", "Comunicado para funcionários não tem turma");
        else if (Input.TurmaId != null && !Turmas.Any(t => t.Value == Input.TurmaId.ToString()))
            ModelState.AddModelError("Input.TurmaId", "Turma inválida");
        DateTime validade = default;
        if (!RegrasFinanceiro.LerData(Input.ValidoAte, out validade)) ModelState.AddModelError("Input.ValidoAte", "Informe a validade (dd/mm/aaaa)");
        else if (validade.Date < DateTime.Today && validade.Date != c?.ValidoAte.Date) ModelState.AddModelError("Input.ValidoAte", "A validade não pode ser no passado");
        else if (validade.Date > DateTime.Today.AddYears(1)) ModelState.AddModelError("Input.ValidoAte", "Use no máximo 1 ano à frente");
        if (!ModelState.IsValid) return Page();

        var agora = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        if (c == null)
        {
            c = new Comunicado
            {
                CriadoEm = agora,
                AutorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : null,
                AutorNome = User.FindFirstValue(ClaimTypes.Name) ?? ""
            };
            _db.Comunicados.Add(c);
        }
        else c.EditadoEm = agora;
        c.Titulo = titulo; c.Texto = texto == "" ? null : texto; c.Link = link == "" ? null : link;
        c.Publico = Input.Publico; c.TurmaId = Input.TurmaId; c.ValidoAte = validade.Date; c.Fixado = Input.Fixado;
        _db.Registrar(User, Auditoria.Comunicados, $"{(id == null ? "Publicou" : "Editou")} o comunicado \"{titulo}\" (para {Input.Publico}, até {validade:dd/MM/yyyy})");
        await _db.SaveChangesAsync();
        TempData["Aviso"] = id == null ? "Comunicado publicado." : "Comunicado atualizado.";
        return RedirectToPage("Index");
    }
}
