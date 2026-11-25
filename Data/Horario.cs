using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

// Horários de aula de um turno (ex.: Manhã, 07:30 às 08:20). Intervalo não recebe disciplina.
public class HorarioAula
{
    public int Id { get; set; }
    public Turno Turno { get; set; }
    public TimeOnly Inicio { get; set; }
    public TimeOnly Fim { get; set; }
    public bool Intervalo { get; set; }
    public string Rotulo => $"{Inicio:HH\\:mm} às {Fim:HH\\:mm}";
}

// Uma célula da grade: a turma tem a disciplina X naquele dia e horário. O professor vem da atribuição da turma (TurmaDisciplina).
public class AulaGrade
{
    public int TurmaId { get; set; }
    public int DiaSemana { get; set; }        // 1 = segunda ... 5 = sexta
    public int HorarioAulaId { get; set; }
    public int DisciplinaId { get; set; }

    public Turma? Turma { get; set; }
    public HorarioAula? Horario { get; set; }
    public Disciplina? Disciplina { get; set; }
}

public static class HorarioExt
{
    public static string Nome(this Turno t) => t switch { Turno.Manha => "Manhã", Turno.Tarde => "Tarde", _ => "Noite" };
}

public record ConflitoHorario(int Dia, int SlotId, int OutraTurmaId, int ProfessorId);

public static class RegrasHorario
{
    public static readonly string[] Dias = { "Segunda", "Terça", "Quarta", "Quinta", "Sexta" };
    public const int MaxAulasSemanais = 30;

    /// <summary>Hora "7:30" ou "07:30". </summary>
    public static bool LerHora(string? txt, out TimeOnly hora) =>
        TimeOnly.TryParseExact((txt ?? "").Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out hora);

    /// <summary>Intervalo de aula válido: fim depois do início.</summary>
    public static string? ValidarIntervalo(TimeOnly ini, TimeOnly fim) => fim <= ini ? "O fim deve ser depois do início" : null;

    /// <summary>Acha horários que se sobrepõem (dentro do mesmo turno). Devolve os índices (na lista dada) dos que colidem com o anterior.</summary>
    public static List<int> Sobrepostos(IReadOnlyList<(TimeOnly Ini, TimeOnly Fim)> horarios)
    {
        var ordem = Enumerable.Range(0, horarios.Count).OrderBy(i => horarios[i].Ini).ToList();
        var ruins = new List<int>();
        for (int k = 1; k < ordem.Count; k++)
            if (horarios[ordem[k]].Ini < horarios[ordem[k - 1]].Fim) ruins.Add(ordem[k]);
        return ruins;
    }

    /// <summary>
    /// Professor em dois lugares ao mesmo tempo: para cada aula proposta da turma, procura o mesmo professor, no mesmo dia e horário, em OUTRA turma.
    /// </summary>
    public static List<ConflitoHorario> Conflitos(
        IEnumerable<(int Dia, int SlotId, int DisciplinaId)> propostas, int turmaId,
        Func<int, int?> professorDaDisciplina,
        IEnumerable<(int Dia, int SlotId, int TurmaId, int ProfessorId)> ocupadasEmOutrasTurmas)
    {
        var lista = ocupadasEmOutrasTurmas.ToList();
        var r = new List<ConflitoHorario>();
        foreach (var (dia, slot, disc) in propostas)
        {
            if (professorDaDisciplina(disc) is not int prof) continue;
            var o = lista.FirstOrDefault(x => x.Dia == dia && x.SlotId == slot && x.ProfessorId == prof && x.TurmaId != turmaId);
            if (o.ProfessorId != 0) r.Add(new ConflitoHorario(dia, slot, o.TurmaId, prof));
        }
        return r;
    }
}

/// <summary>Uma grade pronta para desenhar (partial _Grade): horários nas linhas, dias nas colunas.</summary>
public class GradeView
{
    public List<HorarioAula> Horarios { get; set; } = new();
    public Dictionary<(int Dia, int HorarioId), string[]> Celulas { get; set; } = new();
}

public static class GradesDoProfessor
{
    /// <summary>Uma grade por turno em que o professor tem aula. Cada célula: disciplina e turma.</summary>
    public static async Task<List<(Turno Turno, GradeView Grade)>> MontarAsync(EscolaContext db, int professorId)
    {
        var aulas = await (from g in db.Grade.AsNoTracking()
                           join td in db.Set<TurmaDisciplina>().AsNoTracking() on new { g.TurmaId, g.DisciplinaId } equals new { td.TurmaId, td.DisciplinaId }
                           where td.ProfessorId == professorId
                           select new { g.DiaSemana, g.HorarioAulaId, Disciplina = g.Disciplina!.Nome, g.Turma!.Serie, g.Turma.Letra, g.Turma.AnoLetivo }).ToListAsync();
        var ids = aulas.Select(a => a.HorarioAulaId).Distinct().ToList();
        var horarios = await db.HorariosAula.AsNoTracking().Where(h => ids.Contains(h.Id)).ToListAsync();
        var turnos = horarios.Select(h => h.Turno).Distinct().OrderBy(t => t).ToList();
        var r = new List<(Turno, GradeView)>();
        foreach (var turno in turnos)
        {
            var doTurno = await db.HorariosAula.AsNoTracking().Where(h => h.Turno == turno).OrderBy(h => h.Inicio).ToListAsync();
            var v = new GradeView { Horarios = doTurno };
            foreach (var a in aulas.Where(a => doTurno.Any(h => h.Id == a.HorarioAulaId)))
                v.Celulas[(a.DiaSemana, a.HorarioAulaId)] = new[] { a.Disciplina, $"{a.Serie?.Nome()} {a.Letra} ({a.AnoLetivo})" };
            r.Add((turno, v));
        }
        return r;
    }
}
