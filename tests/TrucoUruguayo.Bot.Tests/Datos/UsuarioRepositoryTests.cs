using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Tests.Helpers;
using Xunit;

namespace TrucoUruguayo.Bot.Tests.Datos;

public class UsuarioRepositoryTests
{
    private readonly string _connectionString = ConexionDePrueba.ObtenerConnectionString();

    [Fact]
    public async Task RegistrarUsuarioAsync_UsuarioNuevo_QuedaConMilMonedasYSePuedeConsultar()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        var id = UsuarioDePrueba.GenerarId();

        await using var usuario = UsuarioDePrueba.ParaLimpieza(_connectionString, id);

        var registrado = await repositorio.RegistrarUsuarioAsync(id, "prueba");
        var consultado = await repositorio.ObtenerUsuarioAsync(id);

        Assert.Equal(1000, registrado.Monedas);
        Assert.Equal(1, registrado.Nivel);
        Assert.NotNull(consultado);
        Assert.Equal(1000, consultado!.Monedas);
        Assert.Equal("prueba", consultado.Nombre);
        Assert.Equal(1, consultado.Nivel);
    }

    [Fact]
    public async Task ObtenerUsuarioAsync_UsuarioInexistente_DevuelveNull()
    {
        var repositorio = new UsuarioRepository(_connectionString);

        var consultado = await repositorio.ObtenerUsuarioAsync(999_999_999);

        Assert.Null(consultado);
    }

    [Theory]
    [InlineData(500, 1500)]
    [InlineData(-300, 700)]
    public async Task ActualizarMonedasAsync_SumaOResta_DejaElSaldoCorrecto(int delta, int esperado)
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 1000);

        await repositorio.ActualizarMonedasAsync(usuario.Id, delta);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(esperado, actualizado!.Monedas);
    }

    [Fact]
    public async Task SumarVictoriaAsync_SumaUnaVictoriaYQuinceXp()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarVictoriaAsync(usuario.Id);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(1, actualizado!.Victorias);
        Assert.Equal(15, actualizado.Xp);
    }

    [Fact]
    public async Task SumarDerrotaAsync_SumaUnaDerrotaYTresXp()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarDerrotaAsync(usuario.Id);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(1, actualizado!.Derrotas);
        Assert.Equal(3, actualizado.Xp);
    }

    [Fact]
    public async Task ReclamarDiariaAsync_PrimeraVez_DaQuinientasMonedasYExito()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 0);

        var (exito, tiempoRestante) = await repositorio.ReclamarDiariaAsync(usuario.Id);

        Assert.True(exito);
        Assert.Null(tiempoRestante);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(500, actualizado!.Monedas);
    }

    [Fact]
    public async Task ReclamarDiariaAsync_SegundaVezAntesDeLas24Horas_NoDaMonedasYDevuelveTiempoRestante()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 0);

        await repositorio.ReclamarDiariaAsync(usuario.Id);
        var (exito, tiempoRestante) = await repositorio.ReclamarDiariaAsync(usuario.Id);

        Assert.False(exito);
        Assert.NotNull(tiempoRestante);
        Assert.True(tiempoRestante!.Value <= TimeSpan.FromHours(24));
        Assert.True(tiempoRestante.Value > TimeSpan.FromHours(23));

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(500, actualizado!.Monedas);
    }

    [Fact]
    public async Task ObtenerTopUsuariosAsync_OrdenXp_OrdenaPorXpDescendente()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuarioBajo = await UsuarioDePrueba.CrearAsync(_connectionString);
        await using var usuarioAlto = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarVictoriaAsync(usuarioBajo.Id);
        for (var i = 0; i < 5; i++)
        {
            await repositorio.SumarVictoriaAsync(usuarioAlto.Id);
        }

        var top = (await repositorio.ObtenerTopUsuariosAsync("xp", limite: 1000)).ToList();
        var posicionAlto = top.FindIndex(u => (ulong)u.Id == usuarioAlto.Id);
        var posicionBajo = top.FindIndex(u => (ulong)u.Id == usuarioBajo.Id);

        Assert.True(posicionAlto < posicionBajo);
    }

    [Fact]
    public async Task RegistrarPartidaAsync_QuedaEnElHistorialDeAmbosJugadores()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var ganador = await UsuarioDePrueba.CrearAsync(_connectionString);
        await using var perdedor = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.RegistrarPartidaAsync(ganador.Id, perdedor.Id, apuesta: 250);

        var historialGanador = await repositorio.ObtenerHistorialAsync(ganador.Id);
        var historialPerdedor = await repositorio.ObtenerHistorialAsync(perdedor.Id);

        Assert.Contains(historialGanador, p => (ulong)p.GanadorId == ganador.Id && (ulong)p.PerdedorId == perdedor.Id);
        Assert.Contains(historialPerdedor, p => (ulong)p.GanadorId == ganador.Id && (ulong)p.PerdedorId == perdedor.Id);
    }
}
