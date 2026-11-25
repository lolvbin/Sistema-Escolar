using System.Numerics;

namespace SistemaEscolar.Data;

// ---------------------------------------------------------------------------------------------
// Gerador automático de horário (regras puras, sem banco). Entra: horários, cargas por disciplina,
// professores, indisponibilidades e o que já está ocupado em turmas que não serão geradas.
// Sai: uma sugestão de grade + o que não coube e por quê. O funcionário confere e aplica.
//
// Regras DURAS (o gerador nunca quebra):
//  1. uma disciplina por horário na turma
//  2. professor em um lugar só por dia e horário (inclusive turmas fora da geração)
//  3. professor não vai nos horários marcados como indisponível
//  4. professor com no máximo N aulas SEGUIDAS (o intervalo quebra a sequência)
//  5. disciplina com no máximo M aulas por dia na turma, e se houver mais de uma no dia, elas ficam coladas (aula dupla)
//  6. cada disciplina recebe exatamente a carga semanal pedida; se não couber, o gerador diz o que faltou
// Regras MOLES (preferências): espalhar a disciplina pelos dias, equilibrar os dias e não deixar buracos.
// ---------------------------------------------------------------------------------------------

/// <summary>Um horário de aula (não intervalo). Ordem = posição no turno contando os intervalos (o intervalo quebra a sequência de aulas seguidas).</summary>
public record SlotAula(int Id, Turno Turno, int Ordem);
public record PedidoAula(int TurmaId, int DisciplinaId, int? ProfessorId, int Carga);
public record AulaProposta(int TurmaId, int Dia, int SlotId, int DisciplinaId, int? ProfessorId);
public record NaoColocada(int TurmaId, int DisciplinaId, int Faltam, string Motivo);

public enum TipoProblema { TurmaExcede, ProfessorExcede, DisciplinaExcede }
/// <summary>Problema visível antes de buscar: pedido maior do que cabe. Necessarias × Disponiveis.</summary>
public record ProblemaGerador(TipoProblema Tipo, int? TurmaId, int? ProfessorId, int? DisciplinaId, int Necessarias, int Disponiveis);

public class EntradaGerador
{
    public List<SlotAula> Slots { get; set; } = new();                               // todos os horários de aula (de todos os turnos)
    public Dictionary<int, List<int>> SlotsDaTurma { get; set; } = new();            // turma → ids dos horários, em ordem
    public List<PedidoAula> Pedidos { get; set; } = new();
    public HashSet<(int Prof, int Dia, int Slot)> Indisponiveis { get; set; } = new();
    public HashSet<(int Prof, int Dia, int Slot)> Ocupados { get; set; } = new();    // turmas fora da geração (ficam como estão)
    public int MaxSeguidas { get; set; } = 3;
    public int MaxPorDia { get; set; } = 2;
    public int Semente { get; set; } = 1;
    public int MaxNos { get; set; } = 1500;      // busca exata, por tentativa (sem relógio: o resultado depende só da semente)
    public int Tentativas { get; set; } = 1;
    public int Recozimentos { get; set; } = 5;   // tentativas da busca local, se a exata não fechou a grade
    public int Iteracoes { get; set; } = 300000; // trocas por recozimento
}

public class ResultadoGerador
{
    public List<AulaProposta> Aulas { get; } = new();
    public List<NaoColocada> NaoColocadas { get; } = new();
    public List<ProblemaGerador> Problemas { get; } = new();
    public bool Completo => NaoColocadas.Count == 0;
    public int Nos { get; set; }
    public int Tentativas { get; set; }
}

public enum TipoViolacao { TurmaDuplicada, ProfessorDuplo, Indisponivel, Seguidas, DisciplinaDia }
public record Violacao(TipoViolacao Tipo, int TurmaId, int Dia, int SlotId, int? ProfessorId, int DisciplinaId);

public static class GeradorHorario
{
    public const int Dias = 5;

    /// <summary>Monta os horários de aula com a Ordem de cada um (posição no turno incluindo intervalos).</summary>
    public static List<SlotAula> SlotsDe(IEnumerable<HorarioAula> horarios)
    {
        var r = new List<SlotAula>();
        foreach (var g in horarios.GroupBy(h => h.Turno))
        {
            int ordem = 0;
            foreach (var h in g.OrderBy(h => h.Inicio))
            {
                if (!h.Intervalo) r.Add(new SlotAula(h.Id, h.Turno, ordem));
                ordem++;
            }
        }
        return r;
    }

    public static ResultadoGerador Gerar(EntradaGerador e) => new Busca(e).Executar();

    // ----------------------------------------------------------------------------------------
    // Verificador independente: confere uma grade pronta contra TODAS as regras duras.
    // (Usado pelos testes, ao aplicar a sugestão e para avisar na grade manual.)
    // ----------------------------------------------------------------------------------------
    public static List<Violacao> Verificar(IReadOnlyCollection<AulaProposta> aulas, IReadOnlyCollection<SlotAula> slots,
        IReadOnlySet<(int Prof, int Dia, int Slot)> indisponiveis, IReadOnlySet<(int Prof, int Dia, int Slot)> ocupadosFixos,
        int maxSeguidas, int maxPorDia)
    {
        var porSlot = slots.ToDictionary(s => s.Id);
        var r = new List<Violacao>();

        foreach (var g in aulas.GroupBy(a => (a.TurmaId, a.Dia, a.SlotId)).Where(g => g.Count() > 1))
            foreach (var a in g.Skip(1)) r.Add(new Violacao(TipoViolacao.TurmaDuplicada, a.TurmaId, a.Dia, a.SlotId, a.ProfessorId, a.DisciplinaId));

        foreach (var g in aulas.Where(a => a.ProfessorId != null).GroupBy(a => (a.ProfessorId!.Value, a.Dia, a.SlotId)))
        {
            var lista = g.ToList();
            foreach (var a in lista.Skip(1)) r.Add(new Violacao(TipoViolacao.ProfessorDuplo, a.TurmaId, a.Dia, a.SlotId, a.ProfessorId, a.DisciplinaId));
            if (ocupadosFixos.Contains(g.Key)) r.Add(new Violacao(TipoViolacao.ProfessorDuplo, lista[0].TurmaId, lista[0].Dia, lista[0].SlotId, lista[0].ProfessorId, lista[0].DisciplinaId));
        }

        foreach (var a in aulas)
            if (a.ProfessorId is int p && indisponiveis.Contains((p, a.Dia, a.SlotId)))
                r.Add(new Violacao(TipoViolacao.Indisponivel, a.TurmaId, a.Dia, a.SlotId, p, a.DisciplinaId));

        // Aulas seguidas: junta as aulas do professor no dia (as dadas + as fixas de outras turmas) e procura sequências longas
        var fixos = new List<(int Prof, int Dia, SlotAula S)>();
        foreach (var (p, d, s) in ocupadosFixos) if (porSlot.TryGetValue(s, out var si)) fixos.Add((p, d, si));
        var proprias = aulas.Where(a => a.ProfessorId != null && porSlot.ContainsKey(a.SlotId))
            .Select(a => (Prof: a.ProfessorId!.Value, a.Dia, S: porSlot[a.SlotId], A: a)).ToList();
        var chaves = proprias.Select(x => (x.Prof, x.Dia, x.S.Turno)).Distinct();
        foreach (var (prof, dia, turno) in chaves)
        {
            var doDia = proprias.Where(x => x.Prof == prof && x.Dia == dia && x.S.Turno == turno).ToList();
            var ordens = doDia.Select(x => x.S.Ordem).Concat(fixos.Where(f => f.Prof == prof && f.Dia == dia && f.S.Turno == turno).Select(f => f.S.Ordem))
                .Distinct().OrderBy(o => o).ToList();
            int ini = 0;
            while (ini < ordens.Count)
            {
                int fim = ini;
                while (fim + 1 < ordens.Count && ordens[fim + 1] == ordens[fim] + 1) fim++;
                if (fim - ini + 1 > maxSeguidas)
                {
                    var minhas = doDia.Where(x => x.S.Ordem >= ordens[ini] && x.S.Ordem <= ordens[fim]).ToList();
                    if (minhas.Count > 0) r.Add(new Violacao(TipoViolacao.Seguidas, minhas[0].A.TurmaId, dia, minhas[0].A.SlotId, prof, minhas[0].A.DisciplinaId));
                }
                ini = fim + 1;
            }
        }

        foreach (var g in aulas.Where(a => porSlot.ContainsKey(a.SlotId)).GroupBy(a => (a.TurmaId, a.DisciplinaId, a.Dia)))
        {
            var ord = g.Select(a => porSlot[a.SlotId].Ordem).OrderBy(o => o).ToList();
            bool colado = ord[^1] - ord[0] + 1 == ord.Count;
            if (ord.Count > maxPorDia || !colado)
            {
                var a = g.First();
                r.Add(new Violacao(TipoViolacao.DisciplinaDia, a.TurmaId, a.Dia, a.SlotId, a.ProfessorId, a.DisciplinaId));
            }
        }
        return r;
    }

    // ----------------------------------------------------------------------------------------
    // A busca: tenta colocar todas as aulas; quando não dá, pula o mínimo possível (e explica).
    // ----------------------------------------------------------------------------------------
    enum Causa { Livre, TurmaOcupada, Ordem, Indisponivel, ProfOcupado, Seguidas, LimiteDia, NaoColada }

    sealed class TurmaEst
    {
        public int Id, N, TurnoInt;
        public int[] SlotIds = Array.Empty<int>(), SlotDenso = Array.Empty<int>(), Ordem = Array.Empty<int>();
        public int[] Grade = Array.Empty<int>();          // dia * N + pos → disciplina (0 = vazio)
        public int[] CargaDia = new int[Dias];
    }

    sealed class Grupo
    {
        public int T, TurmaId, DisciplinaId, Prof = -1;   // Prof = índice denso (-1 sem professor)
        public int? ProfessorId;
        public int Carga, Falta, Ultimo = -1;
        public int[] PorDia = new int[Dias];
        public long[] Ordens = new long[Dias];
    }

    sealed class Busca
    {
        readonly EntradaGerador E;
        readonly Dictionary<int, SlotAula> slotPorId;
        readonly Dictionary<int, int> slotDenso = new();
        readonly Dictionary<int, int> profDenso = new();
        readonly List<TurmaEst> Ts = new();
        readonly List<Grupo> Gs = new();
        readonly List<NaoColocada> semHorario = new();
        readonly List<ProblemaGerador> problemasIniciais = new();
        int S, P, NT;
        bool[] indis = Array.Empty<bool>();
        long[] mascara = Array.Empty<long>();
        readonly List<(int g, int cell, int antigo)> pilha = new();

        int nos, skipped, bestSkipped = int.MaxValue;
        bool abortou, perfeito;
        (int g, int cell)[] melhor = Array.Empty<(int, int)>();
        int[] ordemGrupos = Array.Empty<int>();
        Random rng = new(1);
        double ruido = 1;

        public Busca(EntradaGerador e)
        {
            E = e;
            slotPorId = e.Slots.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            int k = 0;
            foreach (var s in slotPorId.Values.OrderBy(s => s.Id)) slotDenso[s.Id] = k++;
            S = Math.Max(1, k);
            NT = slotPorId.Values.Select(s => (int)s.Turno).DefaultIfEmpty(0).Max() + 1;

            int Prof(int id) { if (!profDenso.TryGetValue(id, out var i)) profDenso[id] = i = profDenso.Count; return i; }

            // Turmas e grupos (uma disciplina de uma turma = um grupo com N aulas). Pedidos repetidos somam.
            foreach (var pg in e.Pedidos.Where(p => p.Carga > 0).GroupBy(p => (p.TurmaId, p.DisciplinaId)))
            {
                var primeiro = pg.First();
                int carga = pg.Sum(p => p.Carga);
                if (!e.SlotsDaTurma.TryGetValue(primeiro.TurmaId, out var slotsT) || slotsT.Count(slotPorId.ContainsKey) == 0)
                {
                    semHorario.Add(new NaoColocada(primeiro.TurmaId, primeiro.DisciplinaId, carga, "o turno da turma não tem horários de aula cadastrados"));
                    continue;
                }
                var t = Ts.FirstOrDefault(x => x.Id == primeiro.TurmaId);
                if (t == null)
                {
                    var ids = slotsT.Where(slotPorId.ContainsKey).ToArray();
                    t = new TurmaEst
                    {
                        Id = primeiro.TurmaId, N = ids.Length, SlotIds = ids,
                        SlotDenso = ids.Select(i => slotDenso[i]).ToArray(), Ordem = ids.Select(i => slotPorId[i].Ordem).ToArray(),
                        TurnoInt = (int)slotPorId[ids[0]].Turno,
                    };
                    t.Grade = new int[Dias * t.N];
                    Ts.Add(t);
                }
                Gs.Add(new Grupo
                {
                    T = Ts.IndexOf(t), TurmaId = primeiro.TurmaId, DisciplinaId = primeiro.DisciplinaId, ProfessorId = primeiro.ProfessorId,
                    Prof = primeiro.ProfessorId is int pid ? Prof(pid) : -1, Carga = carga, Falta = carga,
                });
            }
            // Turma com mais aulas previstas do que horários: corta o excesso das disciplinas de maior carga e avisa
            foreach (var tg in Gs.GroupBy(g => g.T))
            {
                var t = Ts[tg.Key];
                int excesso = tg.Sum(g => g.Carga) - t.N * Dias;
                if (excesso <= 0) continue;
                problemasIniciais.Add(new ProblemaGerador(TipoProblema.TurmaExcede, t.Id, null, null, tg.Sum(g => g.Carga), t.N * Dias));
                var cortes = new Dictionary<Grupo, int>();
                for (int corte = 0; corte < excesso; corte++)
                {
                    var g = tg.OrderByDescending(x => x.Carga).ThenByDescending(x => x.DisciplinaId).First();
                    g.Carga--; g.Falta--; cortes[g] = cortes.GetValueOrDefault(g) + 1;
                }
                foreach (var (g, q) in cortes)
                    semHorario.Add(new NaoColocada(g.TurmaId, g.DisciplinaId, q, "a turma tem mais aulas previstas do que horários na semana"));
            }
            foreach (var (p, _, _) in e.Ocupados) Prof(p);
            foreach (var (p, _, _) in e.Indisponiveis) Prof(p);
            P = Math.Max(1, profDenso.Count);

            indis = new bool[P * Dias * S];
            foreach (var (p, d, s) in e.Indisponiveis)
                if (d >= 1 && d <= Dias && slotDenso.TryGetValue(s, out var si)) indis[(profDenso[p] * Dias + d - 1) * S + si] = true;
            mascara = new long[P * Dias * NT];
            ordemGrupos = Enumerable.Range(0, Gs.Count).ToArray();
        }

        void Reiniciar()
        {
            foreach (var t in Ts) { Array.Clear(t.Grade); Array.Clear(t.CargaDia); }
            foreach (var g in Gs) { g.Falta = g.Carga; g.Ultimo = -1; Array.Clear(g.PorDia); Array.Clear(g.Ordens); }
            Array.Clear(mascara);
            foreach (var (p, d, s) in E.Ocupados)
                if (d >= 1 && d <= Dias && slotPorId.TryGetValue(s, out var si))
                    mascara[(profDenso[p] * Dias + d - 1) * NT + (int)si.Turno] |= 1L << si.Ordem;
            pilha.Clear();
            skipped = 0;
        }

        static int Corrida(long m, int ord)
        {
            int n = 1;
            for (int o = ord - 1; o >= 0 && ((m >> o) & 1) != 0; o--) n++;
            for (int o = ord + 1; o < 63 && ((m >> o) & 1) != 0; o++) n++;
            return n;
        }

        Causa Checar(Grupo g, int cell, bool ignorarOrdem, bool ignorarColada = false)
        {
            var t = Ts[g.T];
            int dia = cell / t.N, pos = cell % t.N, ord = t.Ordem[pos];
            if (t.Grade[cell] != 0) return Causa.TurmaOcupada;
            if (!ignorarOrdem && cell <= g.Ultimo) return Causa.Ordem;
            if (g.Prof >= 0)
            {
                if (indis[(g.Prof * Dias + dia) * S + t.SlotDenso[pos]]) return Causa.Indisponivel;
                long m = mascara[(g.Prof * Dias + dia) * NT + t.TurnoInt];
                if (((m >> ord) & 1) != 0) return Causa.ProfOcupado;
                if (Corrida(m, ord) > E.MaxSeguidas) return Causa.Seguidas;
            }
            if (g.PorDia[dia] >= E.MaxPorDia) return Causa.LimiteDia;
            if (g.PorDia[dia] > 0 && !ignorarColada)
            {
                long o = g.Ordens[dia];
                bool colado = (ord > 0 && ((o >> (ord - 1)) & 1) != 0) || ((o >> (ord + 1)) & 1) != 0;
                if (!colado) return Causa.NaoColada;
            }
            return Causa.Livre;
        }

        void Colocar(int gi, int cell)
        {
            var g = Gs[gi]; var t = Ts[g.T];
            int dia = cell / t.N, pos = cell % t.N, ord = t.Ordem[pos];
            pilha.Add((gi, cell, g.Ultimo));
            t.Grade[cell] = g.DisciplinaId; t.CargaDia[dia]++;
            g.Falta--; g.Ultimo = cell; g.PorDia[dia]++; g.Ordens[dia] |= 1L << ord;
            if (g.Prof >= 0) mascara[(g.Prof * Dias + dia) * NT + t.TurnoInt] |= 1L << ord;
        }

        void Desfazer()
        {
            var (gi, cell, antigo) = pilha[^1]; pilha.RemoveAt(pilha.Count - 1);
            var g = Gs[gi]; var t = Ts[g.T];
            int dia = cell / t.N, pos = cell % t.N, ord = t.Ordem[pos];
            t.Grade[cell] = 0; t.CargaDia[dia]--;
            g.Falta++; g.Ultimo = antigo; g.PorDia[dia]--; g.Ordens[dia] &= ~(1L << ord);
            if (g.Prof >= 0) mascara[(g.Prof * Dias + dia) * NT + t.TurnoInt] &= ~(1L << ord);
        }

        // Devolve as casas onde a PRÓXIMA aula do grupo pode ir agora (estrito) e, à parte, as que só falham por não estarem coladas
        // à aula do mesmo dia (relaxado). As cotas e podas usam o relaxado: colocar uma aula pode abrir casas coladas depois.
        List<int> Dominio(Grupo g, out List<int> relaxado)
        {
            var t = Ts[g.T];
            var estrito = new List<int>(t.N * Dias);
            relaxado = new List<int>(t.N * Dias);
            for (int c = 0; c < t.N * Dias; c++)
            {
                if (Checar(g, c, false, ignorarColada: true) != Causa.Livre) continue;
                relaxado.Add(c);
                if (Checar(g, c, false) == Causa.Livre) estrito.Add(c);
            }
            return estrito;
        }

        int PrimeiroVazio(TurmaEst t, int dia)
        {
            for (int p = 0; p < t.N; p++) if (t.Grade[dia * t.N + p] == 0) return p;
            return t.N;
        }

        bool Dfs()
        {
            if (++nos > E.MaxNos) { abortou = true; return false; }
            int G = Gs.Count;
            var doms = new List<int>?[G];
            var forcados = new List<(int gi, int qtd)>();
            int escolhido = -1, melhorFolga = int.MaxValue, lbGrupos = 0;
            var turmaRest = new int[Ts.Count];
            var turmaUsa = new bool[Ts.Count][];
            var profDem = new int[P];
            var profChaves = new HashSet<int>?[P];

            foreach (int gi in ordemGrupos)
            {
                var g = Gs[gi];
                if (g.Falta == 0) continue;
                var cells = Dominio(g, out var relax);
                if (cells.Count == 0) { forcados.Add((gi, g.Falta)); continue; }   // não cabe nada (e só piora): pula tudo o que falta
                doms[gi] = cells;
                var t = Ts[g.T];
                // capacidade por dia: no máximo MaxPorDia por dia (conta as casas relaxadas)
                int cap = 0;
                var porDia = new int[Dias];
                foreach (var c in relax) porDia[c / t.N]++;
                for (int d = 0; d < Dias; d++) cap += Math.Min(porDia[d], Math.Max(0, E.MaxPorDia - g.PorDia[d]));
                if (cap < g.Falta) lbGrupos += g.Falta - cap;
                int folga = cap - g.Falta;
                if (folga < melhorFolga || (folga == melhorFolga && escolhido >= 0 && g.Falta > Gs[escolhido].Falta)) { melhorFolga = folga; escolhido = gi; }

                turmaRest[g.T] += g.Falta;
                var usa = turmaUsa[g.T] ??= new bool[t.N * Dias];
                foreach (var c in relax) usa[c] = true;
                if (g.Prof >= 0)
                {
                    profDem[g.Prof] += g.Falta;
                    var ch = profChaves[g.Prof] ??= new HashSet<int>();
                    foreach (var c in relax) ch.Add((c / t.N) * S + t.SlotDenso[c % t.N]);
                }
            }
            foreach (var (gi, q) in forcados) { skipped += q; Gs[gi].Falta = 0; }
            void DesfazForcados() { foreach (var (gi, q) in forcados) { skipped -= q; Gs[gi].Falta = q; } }

            int lbProf = 0, lbTurma = 0;
            for (int p = 0; p < P; p++) if (profDem[p] > 0) lbProf += Math.Max(0, profDem[p] - (profChaves[p]?.Count ?? 0));
            for (int ti = 0; ti < Ts.Count; ti++)
                if (turmaRest[ti] > 0) lbTurma += Math.Max(0, turmaRest[ti] - (turmaUsa[ti]?.Count(x => x) ?? 0));
            // forçados já entraram em "skipped"; os grupos que sobraram ainda podem faltar
            int lb = skipped + Math.Max(lbGrupos, Math.Max(lbProf, lbTurma));
            if (lb >= bestSkipped) { DesfazForcados(); return false; }

            if (escolhido < 0)
            {
                // tudo resolvido: achou uma solução melhor que a anterior
                int pulados = skipped;
                bestSkipped = pulados;
                melhor = pilha.Select(x => (x.g, x.cell)).ToArray();
                DesfazForcados();
                if (pulados == 0) { perfeito = true; return true; }
                return false;
            }

            var gE = Gs[escolhido]; var tE = Ts[gE.T];
            var primeiroVazio = new int[Dias];
            for (int d = 0; d < Dias; d++) primeiroVazio[d] = PrimeiroVazio(tE, d);
            var ordenadas = doms[escolhido]!
                .Select(c =>
                {
                    int d = c / tE.N, pos = c % tE.N;
                    double s = gE.PorDia[d] * 100 + tE.CargaDia[d] * 4 + Math.Max(0, pos - primeiroVazio[d]) * 6 + rng.NextDouble() * ruido;
                    return (c, s);
                })
                .OrderBy(x => x.s).Select(x => x.c).ToList();

            foreach (var c in ordenadas)
            {
                Colocar(escolhido, c);
                bool ok = Dfs();
                Desfazer();
                if (ok) { DesfazForcados(); return true; }
                if (abortou) { DesfazForcados(); return false; }
            }
            // última opção: desistir de uma aula desta disciplina
            if (skipped + 1 < bestSkipped)
            {
                gE.Falta--; skipped++;
                bool ok = Dfs();
                gE.Falta++; skipped--;
                if (ok) { DesfazForcados(); return true; }
            }
            DesfazForcados();
            return false;
        }

        // ------------------------------------------------------------------------------------
        // Busca local (recozimento simulado): parte de uma grade COMPLETA (todas as aulas colocadas, mesmo com conflitos)
        // e troca aulas de lugar dentro da turma até zerar as violações. Resolve bem os casos "apertados"
        // (muitos professores compartilhados) em que a busca exata se perde.
        // ------------------------------------------------------------------------------------
        int[] cnt = Array.Empty<int>();
        int[][] cel = Array.Empty<int[]>();
        long[][] tmask = Array.Empty<long[]>();
        int hard, soft;

        int PenSeg(int pi, int dia, int turno)
        {
            long m = mascara[(pi * Dias + dia) * NT + turno];
            int pen = 0;
            while (m != 0)
            {
                int tz = BitOperations.TrailingZeroCount(m);
                m >>= tz;
                int run = BitOperations.TrailingZeroCount(~m);
                if (run > E.MaxSeguidas) pen += run - E.MaxSeguidas;
                m = run >= 63 ? 0 : m >> run;
            }
            return pen;
        }

        (int h, int s) PenGD(Grupo g, int dia)
        {
            int c = g.PorDia[dia];
            if (c == 0) return (0, 0);
            long m = g.Ordens[dia];
            int span = 64 - BitOperations.LeadingZeroCount((ulong)m) - BitOperations.TrailingZeroCount(m);
            return (Math.Max(0, c - E.MaxPorDia) + (span - c), c - 1);
        }

        int Buracos(int t, int dia)
        {
            long m = tmask[t][dia];
            if (m == 0) return 0;
            return 64 - BitOperations.LeadingZeroCount((ulong)m) - BitOperations.PopCount((ulong)m);
        }

        void Por(int t, int cell, int gi)
        {
            if (gi < 0) return;
            var g = Gs[gi]; var T = Ts[t];
            int dia = cell / T.N, pos = cell % T.N, ord = T.Ordem[pos];
            var a = PenGD(g, dia); hard -= a.h; soft -= a.s; soft -= Buracos(t, dia);
            if (g.Prof >= 0) hard -= PenSeg(g.Prof, dia, T.TurnoInt);
            cel[t][cell] = gi; g.PorDia[dia]++; g.Ordens[dia] |= 1L << ord; tmask[t][dia] |= 1L << pos;
            if (g.Prof >= 0)
            {
                int idx = (((g.Prof * Dias + dia) * NT + T.TurnoInt) << 6) + ord;
                if (cnt[idx] >= 1) hard++;
                cnt[idx]++;
                mascara[(g.Prof * Dias + dia) * NT + T.TurnoInt] |= 1L << ord;
                if (indis[(g.Prof * Dias + dia) * S + T.SlotDenso[pos]]) hard++;
                hard += PenSeg(g.Prof, dia, T.TurnoInt);
            }
            a = PenGD(g, dia); hard += a.h; soft += a.s; soft += Buracos(t, dia);
        }

        void Tirar(int t, int cell)
        {
            int gi = cel[t][cell];
            if (gi < 0) return;
            var g = Gs[gi]; var T = Ts[t];
            int dia = cell / T.N, pos = cell % T.N, ord = T.Ordem[pos];
            var a = PenGD(g, dia); hard -= a.h; soft -= a.s; soft -= Buracos(t, dia);
            if (g.Prof >= 0) hard -= PenSeg(g.Prof, dia, T.TurnoInt);
            cel[t][cell] = -1; g.PorDia[dia]--; g.Ordens[dia] &= ~(1L << ord); tmask[t][dia] &= ~(1L << pos);
            if (g.Prof >= 0)
            {
                int idx = (((g.Prof * Dias + dia) * NT + T.TurnoInt) << 6) + ord;
                cnt[idx]--;
                if (cnt[idx] >= 1) hard--; else mascara[(g.Prof * Dias + dia) * NT + T.TurnoInt] &= ~(1L << ord);
                if (indis[(g.Prof * Dias + dia) * S + T.SlotDenso[pos]]) hard--;
                hard += PenSeg(g.Prof, dia, T.TurnoInt);
            }
            a = PenGD(g, dia); hard += a.h; soft += a.s; soft += Buracos(t, dia);
        }

        /// <summary>Uma rodada de recozimento. Devolve true se zerou as violações; deixa a grade em cel[][].</summary>
        bool Recozer(int rodada, (int g, int cell)[] inicial)
        {
            var rnd = new Random(unchecked(E.Semente * 31 + rodada * 7727 + 13));
            Reiniciar();   // zera e carrega o que está fixo (mascara)
            cnt = new int[P * Dias * NT * 64];
            foreach (var (p, d, sl) in E.Ocupados)
                if (d >= 1 && d <= Dias && slotPorId.TryGetValue(sl, out var si)) cnt[((((profDenso[p] * Dias + d - 1) * NT + (int)si.Turno)) << 6) + si.Ordem]++;
            cel = Ts.Select(t => Enumerable.Repeat(-1, t.N * Dias).ToArray()).ToArray();
            tmask = Ts.Select(_ => new long[Dias]).ToArray();
            hard = 0; soft = 0;
            foreach (var g in Gs) { g.Falta = g.Carga; Array.Clear(g.PorDia); Array.Clear(g.Ordens); }

            var faltam = Gs.Select(g => g.Carga).ToArray();
            foreach (var (gi, cell) in inicial) { Por(Gs[gi].T, cell, gi); faltam[gi]--; }
            // o resto das aulas vai para casas vazias ao acaso
            for (int ti = 0; ti < Ts.Count; ti++)
            {
                var vazias = Enumerable.Range(0, Ts[ti].N * Dias).Where(c => cel[ti][c] < 0).OrderBy(_ => rnd.Next()).ToList();
                int k = 0;
                foreach (var gi in Enumerable.Range(0, Gs.Count).Where(i => Gs[i].T == ti))
                    for (int n = 0; n < faltam[gi] && k < vazias.Count; n++) Por(ti, vazias[k++], gi);
            }

            int total = Math.Max(1, Ts.Sum(t => t.N * Dias));
            var pesoTurma = new int[Ts.Count]; // sorteio proporcional ao número de casas
            double t0 = 3.0, t1 = 0.15;
            int iter = Math.Max(1000, E.Iteracoes);
            int extra = iter / 5;
            int feitos = 0; bool zerou = false;
            for (int i = 0; i < iter + extra; i++)
            {
                if (hard == 0 && !zerou) { zerou = true; i = Math.Max(i, iter); }   // zerou: só mais um pouco, a frio, para limpar as preferências
                if (i >= iter && !zerou) break;
                double temp = i < iter ? t0 * Math.Pow(t1 / t0, (double)i / iter) : 0.1;
                int pick = rnd.Next(total), ti = 0;
                while (pick >= Ts[ti].N * Dias) { pick -= Ts[ti].N * Dias; ti++; }
                int n = Ts[ti].N * Dias;
                int a = pick, b = rnd.Next(n);
                if (a == b) continue;
                int ga = cel[ti][a], gb = cel[ti][b];
                if (ga == gb) continue;
                int c0 = hard * 10 + soft, h0 = hard;
                Tirar(ti, a); Tirar(ti, b); Por(ti, a, gb); Por(ti, b, ga);
                int delta = hard * 10 + soft - c0;
                bool aceita = delta <= 0 || (i < iter && rnd.NextDouble() < Math.Exp(-delta / temp));
                if (zerou && hard > 0) aceita = false;   // depois de zerar, nunca volta a quebrar regra
                if (!aceita) { Tirar(ti, a); Tirar(ti, b); Por(ti, a, ga); Por(ti, b, gb); }
                feitos++;
            }
            return hard == 0;
        }

        /// <summary>Transforma a grade do recozimento em uma lista válida: reaplica aula por aula com as regras duras (o que ainda violar fica de fora).</summary>
        void ReaplicarDoRecozimento()
        {
            var cheia = cel.Select(a => (int[])a.Clone()).ToArray();
            Reiniciar();
            for (int ti = 0; ti < Ts.Count; ti++)
                for (int c = 0; c < Ts[ti].N * Dias; c++)
                {
                    int gi = cheia[ti][c];
                    if (gi >= 0 && Checar(Gs[gi], c, false) == Causa.Livre) Colocar(gi, c);
                }
        }

        public ResultadoGerador Executar()
        {
            var r = new ResultadoGerador();
            Reiniciar();   // carrega o que já está ocupado fora da geração
            r.Problemas.AddRange(PreChecar());
            ReduzirExcessos();
            r.NaoColocadas.AddRange(semHorario);
            if (Gs.Count == 0) return r;

            for (int a = 0; a < Math.Max(1, E.Tentativas); a++)
            {
                rng = new Random(unchecked(E.Semente * 7919 + a * 104729));
                ruido = a == 0 ? 1 : Math.Min(60, 3 + a * 5);
                ordemGrupos = Enumerable.Range(0, Gs.Count).ToArray();
                if (a > 0) for (int i = ordemGrupos.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (ordemGrupos[i], ordemGrupos[j]) = (ordemGrupos[j], ordemGrupos[i]); }
                Reiniciar();
                nos = 0; abortou = false;
                Dfs();
                r.Nos += nos; r.Tentativas = a + 1;
                if (perfeito || !abortou) break;   // perfeito, ou busca esgotada (não há solução melhor)
            }

            // a busca exata não fechou a grade: tenta a busca local (só se couber em número de horários)
            int totalCarga = Gs.Sum(g => g.Carga);
            bool cabe = Gs.GroupBy(g => g.T).All(tg => tg.Sum(g => g.Carga) <= Ts[tg.Key].N * Dias);
            if (!perfeito && cabe && (abortou || bestSkipped > 0) && E.Recozimentos > 0)
            {
                var partida = melhor;
                for (int k = 0; k < E.Recozimentos; k++)
                {
                    bool ok = Recozer(k, k == 0 ? partida : Array.Empty<(int, int)>());
                    ReaplicarDoRecozimento();
                    int colocadas = pilha.Count;
                    if (totalCarga - colocadas < bestSkipped) { bestSkipped = totalCarga - colocadas; melhor = pilha.Select(x => (x.g, x.cell)).ToArray(); }
                    r.Tentativas++;
                    if (ok && bestSkipped == 0) break;
                }
            }

            // reconstrói o melhor resultado e explica o que faltou
            Reiniciar();
            foreach (var (gi, cell) in melhor) Colocar(gi, cell);
            foreach (var t in Ts)
                for (int c = 0; c < t.N * Dias; c++)
                    if (t.Grade[c] != 0)
                    {
                        int dia = c / t.N, pos = c % t.N;
                        var g = Gs.First(x => x.T == Ts.IndexOf(t) && x.DisciplinaId == t.Grade[c]);
                        r.Aulas.Add(new AulaProposta(t.Id, dia + 1, t.SlotIds[pos], t.Grade[c], g.ProfessorId));
                    }
            foreach (var g in Gs.Where(x => x.Falta > 0))
                r.NaoColocadas.Add(new NaoColocada(g.TurmaId, g.DisciplinaId, g.Falta, Explicar(g)));
            return r;
        }

        string Explicar(Grupo g)
        {
            var t = Ts[g.T];
            var cont = new Dictionary<Causa, int>();
            int livres = 0;
            for (int c = 0; c < t.N * Dias; c++)
            {
                if (t.Grade[c] != 0) continue;
                livres++;
                var causa = Checar(g, c, true);
                cont[causa] = cont.GetValueOrDefault(causa) + 1;
            }
            if (livres == 0) return "a grade da turma ficou cheia (há mais aulas previstas do que horários)";
            var partes = new List<string>();
            void Add(Causa c, string txt) { if (cont.GetValueOrDefault(c) > 0) partes.Add($"{cont[c]} {txt}"); }
            Add(Causa.ProfOcupado, "com o professor em outra turma");
            Add(Causa.Indisponivel, "com o professor indisponível");
            Add(Causa.Seguidas, $"passariam de {E.MaxSeguidas} aula(s) seguidas do professor");
            Add(Causa.LimiteDia, $"passariam de {E.MaxPorDia} aula(s) da disciplina no mesmo dia");
            Add(Causa.NaoColada, "deixariam a disciplina com aulas separadas no mesmo dia");
            if (cont.GetValueOrDefault(Causa.Livre) > 0) partes.Add($"{cont[Causa.Livre]} serviriam, mas ficaram sem encaixe junto com as outras aulas (tente gerar de novo)");
            return $"dos {livres} horário(s) livres da turma: " + string.Join("; ", partes);
        }

        // Aulas que com certeza não cabem (professor sem horário livre, disciplina acima do limite por dia, professor com mais aulas
        // do que horários livres) saem antes da busca: assim o resto da grade é montado normalmente e o motivo já fica explicado.
        void ReduzirExcessos()
        {
            var relaxados = new Dictionary<Grupo, List<int>>();
            foreach (var g in Gs.Where(x => x.Carga > 0))
            {
                Dominio(g, out var relax);
                relaxados[g] = relax;
                var t = Ts[g.T];
                var porDia = new int[Dias];
                foreach (var c in relax) porDia[c / t.N]++;
                int cap = porDia.Sum(n => Math.Min(n, E.MaxPorDia));
                if (cap >= g.Carga) continue;
                string motivo = cap >= Math.Min(E.MaxPorDia * Dias, t.N * Dias)
                    ? $"com o limite de {E.MaxPorDia} aula(s) da disciplina por dia, só cabem {cap} por semana"
                    : Explicar(g);
                semHorario.Add(new NaoColocada(g.TurmaId, g.DisciplinaId, g.Carga - cap, motivo));
                g.Carga = cap; g.Falta = cap;
            }
            foreach (var pg in Gs.Where(g => g.Prof >= 0 && g.Carga > 0).GroupBy(g => g.Prof))
            {
                var chaves = new HashSet<int>();
                foreach (var g in pg)
                {
                    var t = Ts[g.T];
                    foreach (var c in relaxados[g]) chaves.Add((c / t.N) * S + t.SlotDenso[c % t.N]);
                }
                int excesso = pg.Sum(g => g.Carga) - chaves.Count;
                var cortes = new Dictionary<Grupo, int>();
                for (int k = 0; k < excesso; k++)
                {
                    var g = pg.Where(x => x.Carga > 0).OrderByDescending(x => x.Carga).ThenByDescending(x => x.TurmaId).First();
                    g.Carga--; g.Falta--; cortes[g] = cortes.GetValueOrDefault(g) + 1;
                }
                foreach (var (g, q) in cortes)
                    semHorario.Add(new NaoColocada(g.TurmaId, g.DisciplinaId, q, $"o professor só tem {chaves.Count} horário(s) livres (indisponibilidades e outras turmas) para as aulas previstas"));
            }
        }

        List<ProblemaGerador> PreChecar()
        {
            var l = new List<ProblemaGerador>(problemasIniciais);
            foreach (var g in Gs.Where(g => g.Carga > E.MaxPorDia * Dias))
                l.Add(new ProblemaGerador(TipoProblema.DisciplinaExcede, g.TurmaId, g.ProfessorId, g.DisciplinaId, g.Carga, E.MaxPorDia * Dias));
            foreach (var pg in Gs.Where(g => g.Prof >= 0).GroupBy(g => g.Prof))
            {
                int demanda = pg.Sum(g => g.Carga);
                var disp = new HashSet<(int dia, int slot)>();
                foreach (var g in pg)
                {
                    var t = Ts[g.T];
                    for (int d = 0; d < Dias; d++)
                        for (int p = 0; p < t.N; p++)
                        {
                            if (indis[(g.Prof * Dias + d) * S + t.SlotDenso[p]]) continue;
                            if (((mascara[(g.Prof * Dias + d) * NT + t.TurnoInt] >> t.Ordem[p]) & 1) != 0) continue;   // ocupado fora da geração (a máscara é só dos fixos agora)
                            disp.Add((d, t.SlotIds[p]));
                        }
                }
                if (demanda > disp.Count) l.Add(new ProblemaGerador(TipoProblema.ProfessorExcede, null, pg.First().ProfessorId, null, demanda, disp.Count));
            }
            return l;
        }
    }
}
