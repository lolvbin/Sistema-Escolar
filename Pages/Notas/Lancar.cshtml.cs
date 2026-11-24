using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemaEscolar.Data;

namespace SistemaEscolar.Pages.Notas;

public class LancarModel : PageModel
{
    private readonly EscolaContext _db;
    public LancarModel(EscolaContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int TurmaId { get; set; }
    [BindProperty(SupportsGet = true)] public int DisciplinaId { get; set; }
    [BindProperty(SupportsGet = true)] public int Bim { get; set; } = 1; // 1 a 4 = bimestres; 5 = recuperação

    // Textos digitados por matrícula (aceita vírgula ou ponto nas notas)
    [BindProperty] public Dictionary<int, string?> Valores { get; set; } = new();
    [BindProperty] public Dictionary<int, string?> Faltas { get; set; } = new();
    [BindProperty] public string? Aulas { get; set; }

    public bool Recup => Bim == TabelaNotas.BimRecuperacao;
    public Turma Turma { get; set; } = null!;
    public Disciplina Disciplina { get; set; } = null!;
    public List<Matricula> Alunos { get; set; } = new();
    public Dictionary<int, decimal?[]> Anual { get; set; } = new();   // notas dos 4 bimestres por matrícula
    public Dictionary<int, decimal?> Rec { get; set; } = new();       // nota de recuperação por matrícula
    public Dictionary<int, string> Erros { get; set; } = new();
    public string? ErroAulas { get; set; }

    [TempData] public string? Aviso { get; set; }

    // Confere se o professor logado é o da disciplina nesta turma e carrega os dados
    private async Task<IActionResult?> Carregar()
    {
        if (Bim < 1 || Bim > TabelaNotas.BimRecuperacao) return NotFound();
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var prof = await _db.Professores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == uid && p.Ativo);
        if (prof == null) return Forbid();

        var td = await _db.Set<TurmaDisciplina>().AsNoTracking()
            .Include(x => x.Turma).Include(x => x.Disciplina)
            .FirstOrDefaultAsync(x => x.TurmaId == TurmaId && x.DisciplinaId == DisciplinaId);
        if (td == null) return NotFound();
        if (td.ProfessorId != prof.Id) return Forbid();

        Turma = td.Turma!;
        Disciplina = td.Disciplina!;
        Alunos = await _db.Matriculas.AsNoTracking().Include(m => m.Aluno)
            .Where(m => m.TurmaId == TurmaId && m.Status == StatusMatricula.Ativa)
            .OrderBy(m => m.Aluno!.Nome).ToListAsync();

        var ids = Alunos.Select(a => a.Id).ToList();
        var todas = await _db.Notas.AsNoTracking()
            .Where(n => n.DisciplinaId == DisciplinaId && ids.Contains(n.MatriculaId)).ToListAsync();
        foreach (var m in Alunos)
        {
            var v = new decimal?[TabelaNotas.Bimestres];
            foreach (var n in todas.Where(n => n.MatriculaId == m.Id && n.Bimestre >= 1 && n.Bimestre <= v.Length)) v[n.Bimestre - 1] = n.Valor;
            Anual[m.Id] = v;
            Rec[m.Id] = todas.FirstOrDefault(n => n.MatriculaId == m.Id && n.Bimestre == TabelaNotas.BimRecuperacao)?.Valor;
        }
        // Recuperação: só quem ficou na faixa de recuperação
        if (Recup) Alunos = Alunos.Where(a => Aprovacao.DaDisciplina(Anual[a.Id]) == Situacao.Recuperacao).ToList();
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var r = await Carregar();
        if (r != null) return r;

        var ids = Alunos.Select(a => a.Id).ToList();
        var notas = await _db.Notas.AsNoTracking()
            .Where(n => n.DisciplinaId == DisciplinaId && n.Bimestre == Bim && ids.Contains(n.MatriculaId)).ToListAsync();
        Valores = notas.ToDictionary(n => n.MatriculaId, n => n.Valor.ToString("0.0", CultureInfo.CurrentCulture));

        if (!Recup)
        {
            var aulas = await _db.Aulas.AsNoTracking().FirstOrDefaultAsync(a => a.TurmaId == TurmaId && a.DisciplinaId == DisciplinaId && a.Bimestre == Bim);
            Aulas = aulas?.Aulas.ToString();
            var f = await _db.Frequencias.AsNoTracking()
                .Where(x => x.DisciplinaId == DisciplinaId && x.Bimestre == Bim && ids.Contains(x.MatriculaId)).ToListAsync();
            Faltas = f.ToDictionary(x => x.MatriculaId, x => x.Faltas.ToString());
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await Carregar();
        if (r != null) return r;

        // Valida tudo antes de gravar: se algo estiver errado, nada é salvo
        var novos = new Dictionary<int, decimal?>();
        foreach (var m in Alunos)
        {
            var txt = (Valores.GetValueOrDefault(m.Id) ?? "").Trim();
            if (txt == "") { novos[m.Id] = null; continue; }
            if (!decimal.TryParse(txt.Replace('.', ','), NumberStyles.Number, CultureInfo.CurrentCulture, out var v)
                || v < 0 || v > 10)
            {
                Erros[m.Id] = "Use um número de 0 a 10";
                continue;
            }
            novos[m.Id] = Math.Round(v, 1, MidpointRounding.AwayFromZero);
        }

        int aulas = 0;
        var faltas = new Dictionary<int, int?>();
        if (!Recup)
        {
            var at = (Aulas ?? "").Trim();
            if (at != "" && (!int.TryParse(at, out aulas) || aulas < 0 || aulas > 300)) ErroAulas = "Use um número inteiro de 0 a 300";
            foreach (var m in Alunos)
            {
                var ft = (Faltas.GetValueOrDefault(m.Id) ?? "").Trim();
                if (ft == "") { faltas[m.Id] = null; continue; }
                if (!int.TryParse(ft, out var f) || f < 0) { Erros[m.Id] = "Faltas: número inteiro, 0 ou mais"; continue; }
                if (ErroAulas == null && f > aulas) { Erros[m.Id] = aulas == 0 ? "Informe as aulas dadas" : "Faltas maior que as aulas dadas"; continue; }
                faltas[m.Id] = f;
            }
        }
        if (Erros.Count > 0 || ErroAulas != null) return Page();

        var ids = Alunos.Select(a => a.Id).ToList();
        var atuais = await _db.Notas
            .Where(n => n.DisciplinaId == DisciplinaId && n.Bimestre == Bim && ids.Contains(n.MatriculaId))
            .ToDictionaryAsync(n => n.MatriculaId);
        var mudancas = new List<string>();
        foreach (var (mid, valor) in novos)
        {
            atuais.TryGetValue(mid, out var n);
            if (n?.Valor != valor)
                mudancas.Add(Auditoria.MudancaNota(Alunos.First(a => a.Id == mid).Aluno?.Nome ?? $"matrícula {mid}", n?.Valor, valor));
            if (valor == null) { if (n != null) _db.Notas.Remove(n); }
            else if (n == null) _db.Notas.Add(new Nota { MatriculaId = mid, DisciplinaId = DisciplinaId, Bimestre = Bim, Valor = valor.Value });
            else n.Valor = valor.Value;
        }

        if (!Recup)
        {
            var fAtuais = await _db.Frequencias
                .Where(x => x.DisciplinaId == DisciplinaId && x.Bimestre == Bim && ids.Contains(x.MatriculaId))
                .ToDictionaryAsync(x => x.MatriculaId);
            foreach (var (mid, f) in faltas)
            {
                fAtuais.TryGetValue(mid, out var x);
                if (f == null) { if (x != null) _db.Frequencias.Remove(x); }
                else if (x == null) _db.Frequencias.Add(new Frequencia { MatriculaId = mid, DisciplinaId = DisciplinaId, Bimestre = Bim, Faltas = f.Value });
                else x.Faltas = f.Value;
            }
            var a = await _db.Aulas.FirstOrDefaultAsync(x => x.TurmaId == TurmaId && x.DisciplinaId == DisciplinaId && x.Bimestre == Bim);
            if (aulas == 0) { if (a != null) _db.Aulas.Remove(a); }
            else if (a == null) _db.Aulas.Add(new AulasDadas { TurmaId = TurmaId, DisciplinaId = DisciplinaId, Bimestre = Bim, Aulas = aulas });
            else a.Aulas = aulas;
        }
        if (mudancas.Count > 0)
            _db.Registrar(User, Auditoria.Notas,
                $"{Disciplina.Nome}, {(Recup ? "recuperação" : Bim + "º bim.")}: {string.Join("; ", mudancas)}");
        await _db.SaveChangesAsync();

        Aviso = Recup ? "Notas de recuperação salvas." : $"Dados do {Bim}º bimestre salvos.";
        return RedirectToPage(new { TurmaId, DisciplinaId, Bim });
    }
}
