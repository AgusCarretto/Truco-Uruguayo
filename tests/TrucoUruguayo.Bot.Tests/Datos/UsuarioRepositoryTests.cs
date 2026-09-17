using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Servicios;
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
        Assert.Null(registrado.TituloEquipado);
        Assert.Equal(GeneradorImagenes.FondoMadera, registrado.FondoEquipado);
        Assert.NotNull(consultado);
        Assert.Equal(1000, consultado!.Monedas);
        Assert.Equal("prueba", consultado.Nombre);
        Assert.Equal(1, consultado.Nivel);
        Assert.Null(consultado.TituloEquipado);
        Assert.Equal(GeneradorImagenes.FondoMadera, consultado.FondoEquipado);
    }

    [Fact]
    public async Task ObtenerUsuarioAsync_UsuarioInexistente_DevuelveNull()
    {
        var repositorio = new UsuarioRepository(_connectionString);

        var consultado = await repositorio.ObtenerUsuarioAsync(999_999_999);

        Assert.Null(consultado);
    }

    [Fact]
    public async Task EquiparTituloAsync_ConNivelSuficiente_EquipaYDevuelveTrue()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);
        await repositorio.SumarExpAsync(usuario.Id, 1000); // llega a Nivel 5

        var exito = await repositorio.EquiparTituloAsync(usuario.Id, "Orejeador");

        Assert.True(exito);
        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal("Orejeador", actualizado!.TituloEquipado);
    }

    [Fact]
    public async Task EquiparTituloAsync_ConNivelInsuficiente_NoEquipaYDevuelveFalse()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var exito = await repositorio.EquiparTituloAsync(usuario.Id, "Orejeador");

        Assert.False(exito);
        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Null(actualizado!.TituloEquipado);
    }

    [Fact]
    public async Task EquiparTituloAsync_TituloInexistente_DevuelveFalse()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var exito = await repositorio.EquiparTituloAsync(usuario.Id, "Titulo Inventado");

        Assert.False(exito);
    }

    [Fact]
    public async Task EquiparFondoAsync_FondoMadera_SiempreExitosoSinNecesidadDeComprarlo()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var exito = await repositorio.EquiparFondoAsync(usuario.Id, GeneradorImagenes.FondoMadera);

        Assert.True(exito);
        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(GeneradorImagenes.FondoMadera, actualizado!.FondoEquipado);
    }

    [Fact]
    public async Task EquiparFondoAsync_FondoComprableSinComprar_DevuelveFalse()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var exito = await repositorio.EquiparFondoAsync(usuario.Id, GeneradorImagenes.FondoPoker);

        Assert.False(exito);
        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(GeneradorImagenes.FondoMadera, actualizado!.FondoEquipado);
    }

    [Fact]
    public async Task EquiparFondoAsync_FondoComprado_EquipaYDevuelveTrue()
    {
        var repositorioUsuarios = new UsuarioRepository(_connectionString);
        var repositorioTienda = new TiendaRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 10_000);
        await using var item = await ItemDePrueba.CrearAsync(_connectionString, precio: 8000, nombre: "Fondo Póker");
        await repositorioTienda.ComprarItemAsync(usuario.Id, item.Id);

        var exito = await repositorioUsuarios.EquiparFondoAsync(usuario.Id, GeneradorImagenes.FondoPoker);

        Assert.True(exito);
        var actualizado = await repositorioUsuarios.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(GeneradorImagenes.FondoPoker, actualizado!.FondoEquipado);
    }

    [Fact]
    public async Task EquiparFondoAsync_FondoInvalido_DevuelveFalse()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var exito = await repositorio.EquiparFondoAsync(usuario.Id, "fondo_inventado");

        Assert.False(exito);
    }

    [Fact]
    public async Task RegistrarProgresoAsync_AlcanzaLaMeta_DesbloqueaElLogroYSumaLasMonedas()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 0);

        var logros = await repositorio.RegistrarProgresoAsync(usuario.Id, "partidas_ganadas");

        Assert.Contains(logros, l => l.Nombre == "Primer Triunfo");
        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(500, actualizado!.Monedas);
    }

    [Fact]
    public async Task RegistrarProgresoAsync_NoAlcanzaLaMeta_NoDesbloqueaNada()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var logros = await repositorio.RegistrarProgresoAsync(usuario.Id, "flores_cantadas");

        Assert.Empty(logros);
    }

    [Fact]
    public async Task RegistrarProgresoAsync_LogroYaDesbloqueado_NoLoRepiteNiSumaMonedasDeNuevo()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 0);
        await repositorio.RegistrarProgresoAsync(usuario.Id, "partidas_ganadas");

        var logros = await repositorio.RegistrarProgresoAsync(usuario.Id, "partidas_ganadas");

        Assert.Empty(logros);
        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(500, actualizado!.Monedas);
    }

    [Fact]
    public async Task RegistrarProgresoAsync_EstadisticaInvalida_TiraExcepcion()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var excepcion = await Record.ExceptionAsync(() => repositorio.RegistrarProgresoAsync(usuario.Id, "estadistica_inventada"));

        Assert.IsType<ArgumentOutOfRangeException>(excepcion);
    }

    [Fact]
    public async Task ObtenerInsigniasAsync_ConLogroDesbloqueado_DevuelveElEmoji()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);
        await repositorio.RegistrarProgresoAsync(usuario.Id, "partidas_ganadas");

        var insignias = await repositorio.ObtenerInsigniasAsync(usuario.Id);

        Assert.Contains("🏆", insignias);
    }

    [Fact]
    public async Task ObtenerInsigniasAsync_SinLogros_DevuelveListaVacia()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var insignias = await repositorio.ObtenerInsigniasAsync(usuario.Id);

        Assert.Empty(insignias);
    }

    [Fact]
    public async Task SumarExpAsync_SinCruzarUmbral_NoSubeDeNivel()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var (subioDeNivel, nuevoNivel) = await repositorio.SumarExpAsync(usuario.Id, 50);

        Assert.False(subioDeNivel);
        Assert.Equal(1, nuevoNivel);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(50, actualizado!.Xp);
        Assert.Equal(1, actualizado.Nivel);
    }

    [Fact]
    public async Task SumarExpAsync_CruzaUnUmbral_SubeDeNivel()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var (subioDeNivel, nuevoNivel) = await repositorio.SumarExpAsync(usuario.Id, 150);

        Assert.True(subioDeNivel);
        Assert.Equal(2, nuevoNivel);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(150, actualizado!.Xp);
        Assert.Equal(2, actualizado.Nivel);
    }

    [Fact]
    public async Task SumarExpAsync_CruzaVariosUmbrales_SubeVariosNiveles()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var (subioDeNivel, nuevoNivel) = await repositorio.SumarExpAsync(usuario.Id, 500);

        Assert.True(subioDeNivel);
        Assert.Equal(3, nuevoNivel);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(500, actualizado!.Xp);
        Assert.Equal(3, actualizado.Nivel);
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
    public async Task SumarVictoriaAsync_SumaUnaVictoriaYNoTocaElXp()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarVictoriaAsync(usuario.Id);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(1, actualizado!.Victorias);
        Assert.Equal(0, actualizado.Xp);
    }

    [Fact]
    public async Task SumarDerrotaAsync_SumaUnaDerrotaYNoTocaElXp()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarDerrotaAsync(usuario.Id);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(1, actualizado!.Derrotas);
        Assert.Equal(0, actualizado.Xp);
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

        await repositorio.SumarExpAsync(usuarioBajo.Id, 50);
        await repositorio.SumarExpAsync(usuarioAlto.Id, 250);

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
