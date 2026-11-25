using Microsoft.EntityFrameworkCore;

namespace SistemaEscolar.Data;

/// <summary>Tudo o que o gerador precisa do banco, já no formato dele, mais os nomes para mostrar na tela.</summary>
public class ContextoGerador
{
    public EntradaGerador Entrada { get; set; } = new();
    public List<Turma> Turmas { get; set; } = new();                       // turmas que entram na geração (ativas, do ano, com carga)
    public List<Turma> SemCarga { get; set; } = new();                     // escolhidas, mas sem "aulas/semana" definidas
    public Dictionary<int, string> Disciplinas { get; set; } = new();
    public Dictionary<int, string> Professores { get; set; } = new();
    public Dictionary<(int TurmaId, int DisciplinaId), int?> ProfessorDe { get; set; } = new();   // todas as disciplinas das turmas
    public Dictionary<Turno, List<HorarioAula>> HorariosPorTurno { get; set; } = new();           // com intervalos, para desenhar
}

public static class GeradorHorarioDb
{
    public static async Task<ContextoGerador> CarregarAsync(EscolaContext db, int ano, IReadOnlyCollection<int> turmaIds)
    {
        var c = new ContextoGerador();
        var e = c.Entrada;
        e.MaxSeguidas = RegrasHorario.MaxSeguidas;
        e.MaxPorDia = RegrasHorario.MaxPorDia;

        var horarios = await db.HorariosAula.AsNoTracking().OrderBy(h => h.Turno).ThenBy(h => h.Inicio).ToListAsync();
        c.HorariosPorTurno = horarios.GroupBy(h => h.Turno).ToDictionary(g => g.Key, g => g.ToList());
        e.Slots = GeradorHorario.SlotsDe(horarios);

        var turmas = await db.Turmas.AsNoTracking().Where(t => t.Ativa && t.AnoLetivo == ano && turmaIds.Contains(t.Id))
            .OrderBy(t => t.Serie).ThenBy(t => t.Letra).ToListAsync();
        var ids = turmas.Select(t => t.Id).ToList();
        var tds = await db.Set<TurmaDisciplina>().AsNoTracking().Include(x => x.Disciplina).Include(x => x.Professor)
            .Where(x => ids.Contains(x.TurmaId)).ToListAsync();
        foreach (var td in tds)
        {
            c.Disciplinas[td.DisciplinaId] = td.Disciplina!.Nome;
            if (td.Professor != null) c.Professores[td.Professor.Id] = td.Professor.Nome;
            c.ProfessorDe[(td.TurmaId, td.DisciplinaId)] = td.ProfessorId;
        }

        foreach (var t in turmas)
        {
            if (!tds.Any(x => x.TurmaId == t.Id && x.AulasSemanais > 0)) { c.SemCarga.Add(t); continue; }
            c.Turmas.Add(t);
            e.SlotsDaTurma[t.Id] = e.Slots.Where(s => s.Turno == t.Turno).OrderBy(s => s.Ordem).Select(s => s.Id).ToList();
        }
        foreach (var td in tds.Where(x => x.AulasSemanais > 0 && c.Turmas.Any(t => t.Id == x.TurmaId)))
            e.Pedidos.Add(new PedidoAula(td.TurmaId, td.DisciplinaId, td.ProfessorId, td.AulasSemanais));

        var profs = tds.Where(x => x.ProfessorId != null).Select(x => x.ProfessorId!.Value).Distinct().ToList();
        foreach (var i in await db.Indisponibilidades.AsNoTracking().Where(x => profs.Contains(x.ProfessorId)).ToListAsync())
            e.Indisponiveis.Add((i.ProfessorId, i.DiaSemana, i.HorarioAulaId));

        // Aulas de outras turmas do mesmo ano (fora da geração) continuam como estão e ocupam os professores
        var fixas = await (from g in db.Grade.AsNoTracking()
                           join td in db.Set<TurmaDisciplina>().AsNoTracking() on new { g.TurmaId, g.DisciplinaId } equals new { td.TurmaId, td.DisciplinaId }
                           where !ids.Contains(g.TurmaId) && g.Turma!.AnoLetivo == ano && g.Turma.Ativa && td.ProfessorId != null && profs.Contains(td.ProfessorId.Value)
                           select new { td.ProfessorId, g.DiaSemana, g.HorarioAulaId }).ToListAsync();
        foreach (var f in fixas) e.Ocupados.Add((f.ProfessorId!.Value, f.DiaSemana, f.HorarioAulaId));
        return c;
    }

    /// <summary>Confere uma sugestão (vinda da tela) contra o banco de agora. Devolve o que está errado; vazio = pode aplicar.</summary>
    public static async Task<List<string>> ValidarPlanoAsync(EscolaContext db, int ano, Dictionary<int, List<(int Dia, int SlotId, int DisciplinaId)>> plano)
    {
        var erros = new List<string>();
        var c = await CarregarAsync(db, ano, plano.Keys.ToList());
        var turmas = await db.Turmas.AsNoTracking().Where(t => t.Ativa && t.AnoLetivo == ano && plano.Keys.Contains(t.Id)).ToListAsync();
        if (turmas.Count != plano.Count) erros.Add("Alguma turma da sugestão não existe mais ou foi inativada");

        var aulas = new List<AulaProposta>();
        foreach (var t in turmas)
        {
            var slotsDaTurma = c.Entrada.Slots.Where(s => s.Turno == t.Turno).Select(s => s.Id).ToHashSet();
            foreach (var (dia, slot, disc) in plano[t.Id])
            {
                if (dia < 1 || dia > 5 || !slotsDaTurma.Contains(slot)) { erros.Add($"Horário inválido na turma {t.Nome}"); continue; }
                if (!c.ProfessorDe.TryGetValue((t.Id, disc), out var prof)) { erros.Add($"Disciplina que não é da turma {t.Nome}"); continue; }
                aulas.Add(new AulaProposta(t.Id, dia, slot, disc, prof));
            }
        }
        if (erros.Count > 0) return erros.Distinct().ToList();
        var v = GeradorHorario.Verificar(aulas, c.Entrada.Slots, c.Entrada.Indisponiveis, c.Entrada.Ocupados, c.Entrada.MaxSeguidas, c.Entrada.MaxPorDia);
        if (v.Count > 0) erros.Add($"{v.Count} problema(s) com os dados de agora (professor, indisponibilidade ou limites mudaram depois da geração)");
        return erros;
    }
}
