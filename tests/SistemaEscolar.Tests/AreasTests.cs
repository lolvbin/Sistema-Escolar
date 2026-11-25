using System.Security.Claims;
using SistemaEscolar.Data;

namespace SistemaEscolar.Tests;

public class AreasTests
{
    static ClaimsPrincipal Func(Area areas) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, nameof(Perfil.Funcionario)),
        new Claim(AreaExt.ClaimAreas, ((int)areas).ToString())
    }, "teste"));

    [Fact]
    public void Administrador_tem_todas_as_areas()
    {
        Assert.Equal(AreaExt.Todas, Cargo.Administrador.Padrao());
        foreach (var a in AreaExt.Individuais) Assert.True(Cargo.Administrador.Padrao().HasFlag(a));
    }

    [Fact]
    public void Somente_o_administrador_tem_configuracoes_e_usuarios()
    {
        foreach (var c in new[] { Cargo.Secretaria, Cargo.Financeiro, Cargo.Coordenacao })
        {
            Assert.False(c.Padrao().HasFlag(Area.Configuracoes), c.ToString());
            Assert.False(c.Padrao().HasFlag(Area.Usuarios), c.ToString());
        }
    }

    [Fact]
    public void Cargos_tem_as_areas_combinadas()
    {
        Assert.Equal(Area.Financeiro | Area.Calendario, Cargo.Financeiro.Padrao());
        Assert.True(Cargo.Secretaria.Padrao().HasFlag(Area.Matriculas));
        Assert.False(Cargo.Secretaria.Padrao().HasFlag(Area.Financeiro));
        Assert.True(Cargo.Coordenacao.Padrao().HasFlag(Area.Desempenho));
        Assert.False(Cargo.Coordenacao.Padrao().HasFlag(Area.Matriculas));
    }

    [Fact]
    public void Os_valores_das_areas_sao_bits_distintos()
    {
        var valores = AreaExt.Individuais.Select(a => (int)a).ToList();
        Assert.All(valores, v => Assert.Equal(1, System.Numerics.BitOperations.PopCount((uint)v)));
        Assert.Equal(valores.Count, valores.Distinct().Count());
        Assert.Equal(2047, (int)AreaExt.Todas); // a migration AreasDoFuncionario usou 1023 (as 10 primeiras); Comunicados (1024) veio depois, com a própria migration
    }

    [Fact]
    public void TemArea_le_o_claim_e_exige_o_perfil_funcionario()
    {
        var u = Func(Area.Financeiro);
        Assert.True(u.TemArea(Area.Financeiro));
        Assert.False(u.TemArea(Area.Matriculas));

        var professor = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, nameof(Perfil.Professor)),
            new Claim(AreaExt.ClaimAreas, ((int)AreaExt.Todas).ToString())   // mesmo com o claim, só funcionário vale
        }, "teste"));
        Assert.False(professor.TemArea(Area.Financeiro));
    }

    [Fact]
    public void Principal_do_funcionario_leva_as_areas_no_cookie()
    {
        var u = new Usuario { Id = 1, Nome = "X", Email = "x@x", Perfil = Perfil.Funcionario, Areas = Area.Turmas | Area.Alunos };
        var p = Autenticacao.CriarPrincipal(u);
        Assert.Equal(Area.Turmas | Area.Alunos, p.Areas());
        Assert.Equal(Area.Nenhuma, Autenticacao.CriarPrincipal(new Usuario { Id = 2, Nome = "P", Email = "p@p", Perfil = Perfil.Professor }).Areas());
    }

    [Fact]
    public void Sistema_nunca_fica_sem_administrador_ativo()
    {
        Assert.True(RegrasUsuarios.RestaAdministrador(new[] { (true, Area.Usuarios | Area.Alunos), (true, Area.Alunos) }));
        Assert.False(RegrasUsuarios.RestaAdministrador(new[] { (false, Area.Usuarios), (true, Area.Alunos) }));   // o único com Usuários está inativo
        Assert.False(RegrasUsuarios.RestaAdministrador(new[] { (true, Area.Alunos) }));
        Assert.False(RegrasUsuarios.RestaAdministrador(Array.Empty<(bool, Area)>()));
    }

    [Fact]
    public void Resumo_das_areas()
    {
        Assert.Equal("Todas", AreaExt.Todas.Resumo());
        Assert.Equal("nenhuma", Area.Nenhuma.Resumo());
        Assert.Equal("Financeiro, Calendário", (Area.Financeiro | Area.Calendario).Resumo());
    }
}

public class SessoesTests
{
    static System.Security.Claims.ClaimsPrincipal Usuario(int id) =>
        new(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, id.ToString()) }, "t"));

    [Fact]
    public void Cookie_emitido_antes_da_revogacao_e_rejeitado()
    {
        var antes = DateTimeOffset.UtcNow.AddMinutes(-5);
        Assert.False(Sessoes.Invalida(Usuario(910001), antes));   // ninguém revogou
        Sessoes.Revogar(910001);
        Assert.True(Sessoes.Invalida(Usuario(910001), antes));
        Assert.False(Sessoes.Invalida(Usuario(910001), DateTimeOffset.UtcNow.AddMinutes(1))); // novo login depois vale
        Assert.False(Sessoes.Invalida(Usuario(910001), DateTimeOffset.UtcNow));               // entrou de novo no mesmo segundo
        Assert.False(Sessoes.Invalida(Usuario(910002), antes));   // outro usuário não é afetado
    }
}
