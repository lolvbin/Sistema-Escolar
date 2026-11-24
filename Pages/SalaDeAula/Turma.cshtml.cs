using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.SalaDeAula;

// Professor publica avisos, materiais e atividades de uma turma, na disciplina que ele leciona.
public class TurmaModel : PageModel
{
    private readonly EscolaContext _db;
    public TurmaModel(EscolaContext db) => _db = db;

    public class Entrada
    {
        public TipoPublicacao? Tipo { get; set; }
        public string? Titulo { get; set; }
        public string? Texto { get; set; }
        public string? Link { get; set; }
        public string? Prazo { get; set; }   // dd/MM/yyyy
    }

    [BindProperty] public Entrada Input { get; set; } = new();

    public Data.Turma Turma { get; set; } = null!;
    public Disciplina Disciplina { get; set; } = null!;
    public List<Publicacao> Itens { get; set; } = new();
    [TempData] public string? Aviso { get; set; }

    private Professor? _prof;

    // Só entra quem é o professor atribuído a essa disciplina nessa turma
    private async Task<IActionResult?> Carregar(int turmaId, int disciplinaId)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        _prof = await _db.Professores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == uid && p.Ativo);
        var td = await _db.Set<TurmaDisciplina>().AsNoTracking().Include(x => x.Turma).Include(x => x.Disciplina)
            .FirstOrDefaultAsync(x => x.TurmaId == turmaId && x.DisciplinaId == disciplinaId);
        if (td == null) return NotFound();
        if (_prof == null || td.ProfessorId != _prof.Id) return Forbid();
        Turma = td.Turma!; Disciplina = td.Disciplina!;
        Itens = await _db.Publicacoes.AsNoTracking()
            .Where(p => p.TurmaId == turmaId && p.DisciplinaId == disciplinaId)
            .OrderByDescending(p => p.CriadoEm).ToListAsync();
        return null;
    }

    public async Task<IActionResult> OnGetAsync(int turmaId, int disciplinaId) =>
        await Carregar(turmaId, disciplinaId) ?? Page();

    public async Task<IActionResult> OnPostPublicarAsync(int turmaId, int disciplinaId)
    {
        var r = await Carregar(turmaId, disciplinaId);
        if (r != null) return r;

        var titulo = (Input.Titulo ?? "").Trim();
        var texto = (Input.Texto ?? "").Trim();
        var link = (Input.Link ?? "").Trim();
        if (Input.Tipo == null) ModelState.AddModelError("Input.Tipo", "Selecione o tipo");
        if (titulo == "") ModelState.AddModelError("Input.Titulo", "Informe o título");
        else if (titulo.Length > 100) ModelState.AddModelError("Input.Titulo", "Use até 100 caracteres");
        if (texto.Length > 2000) ModelState.AddModelError("Input.Texto", "Use até 2000 caracteres");
        if (Input.Tipo != TipoPublicacao.Atividade && texto == "" && link == "")
            ModelState.AddModelError("Input.Texto", "Escreva o texto ou informe um link");
        if (link != "" && !Publicacao.LinkValido(link))
            ModelState.AddModelError("Input.Link", "Link inválido. Use um endereço começando com http:// ou https://");
        DateTime? prazo = null;
        if (Input.Tipo == TipoPublicacao.Atividade)
        {
            if (!RegrasFinanceiro.LerData(Input.Prazo, out var p)) ModelState.AddModelError("Input.Prazo", "Informe o prazo (dd/mm/aaaa)");
            else if (p.Date < DateTime.Today) ModelState.AddModelError("Input.Prazo", "O prazo não pode ser no passado");
            else prazo = p.Date;
        }
        if (!ModelState.IsValid) return Page();

        _db.Publicacoes.Add(new Publicacao
        {
            TurmaId = turmaId, DisciplinaId = disciplinaId, ProfessorId = _prof!.Id, Tipo = Input.Tipo!.Value,
            Titulo = titulo, Texto = texto == "" ? null : texto, Link = link == "" ? null : link, Prazo = prazo,
            CriadoEm = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
        });
        await _db.SaveChangesAsync();
        Aviso = "Publicado.";
        return RedirectToPage(new { turmaId, disciplinaId });
    }

    public async Task<IActionResult> OnPostExcluirAsync(int turmaId, int disciplinaId, int id)
    {
        var r = await Carregar(turmaId, disciplinaId);
        if (r != null) return r;
        var p = await _db.Publicacoes.FirstOrDefaultAsync(x => x.Id == id && x.TurmaId == turmaId && x.DisciplinaId == disciplinaId);
        if (p == null) return NotFound();
        _db.Publicacoes.Remove(p);
        await _db.SaveChangesAsync();
        Aviso = "Publicação excluída.";
        return RedirectToPage(new { turmaId, disciplinaId });
    }
}
