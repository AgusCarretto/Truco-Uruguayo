using Discord.WebSocket;
using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Servicios;
using Xunit;

namespace TrucoUruguayo.Bot.Tests.Servicios;

public class GestorPartidasTests
{
    // GestorPartidas necesita un DiscordSocketClient y un UsuarioRepository para el timer de AFK
    // (ver GestorPartidas.cs), pero ninguno de los dos hace falta conectado para estos tests:
    // no esperamos a que el timer dispare (corre cada 10s, estos tests terminan en milisegundos).
    private static GestorPartidas CrearGestor(TimeSpan? limiteReto = null) =>
        new(new DiscordSocketClient(), new UsuarioRepository("Host=localhost"), limiteReto);


    [Fact]
    public void IniciarPartida_AmbosJugadoresLibres_CreaLaPartidaYLosMarcaActivos()
    {
        var gestor = CrearGestor();

        var resultado = gestor.IniciarPartida(canalId: 1, jugador1Id: 10, jugador2Id: 20, puntosObjetivo: 15);

        Assert.True(resultado);
        Assert.True(gestor.PartidasActivas.ContainsKey(1));
        Assert.Equal(1UL, gestor.JugadoresActivos[10]);
        Assert.Equal(1UL, gestor.JugadoresActivos[20]);
    }

    [Fact]
    public void IniciarPartida_UnJugadorYaEstaEnOtraPartida_NoCreaLaPartidaYDevuelveFalse()
    {
        var gestor = CrearGestor();
        gestor.IniciarPartida(canalId: 1, jugador1Id: 10, jugador2Id: 20, puntosObjetivo: 15);

        var resultado = gestor.IniciarPartida(canalId: 2, jugador1Id: 10, jugador2Id: 30, puntosObjetivo: 15);

        Assert.False(resultado);
        Assert.False(gestor.PartidasActivas.ContainsKey(2));
        Assert.False(gestor.JugadoresActivos.ContainsKey(30));
        Assert.Equal(1UL, gestor.JugadoresActivos[10]);
    }

    [Fact]
    public void IniciarPartida_SegundoJugadorYaEstaEnOtraPartida_LiberaAlPrimeroYDevuelveFalse()
    {
        var gestor = CrearGestor();
        gestor.IniciarPartida(canalId: 1, jugador1Id: 10, jugador2Id: 20, puntosObjetivo: 15);

        var resultado = gestor.IniciarPartida(canalId: 2, jugador1Id: 30, jugador2Id: 20, puntosObjetivo: 15);

        Assert.False(resultado);
        Assert.False(gestor.JugadoresActivos.ContainsKey(30));
        Assert.False(gestor.PartidasActivas.ContainsKey(2));
    }

    [Fact]
    public void ObtenerPartidaPorUsuario_JugadorEnPartida_DevuelveLaRonda()
    {
        var gestor = CrearGestor();
        gestor.IniciarPartida(canalId: 1, jugador1Id: 10, jugador2Id: 20, puntosObjetivo: 15);

        var ronda = gestor.ObtenerPartidaPorUsuario(10);

        Assert.NotNull(ronda);
        Assert.Equal(10UL, ronda!.Jugador1Id);
        Assert.Equal(20UL, ronda.Jugador2Id);
    }

    [Fact]
    public void ObtenerPartidaPorUsuario_JugadorSinPartida_DevuelveNull()
    {
        var gestor = CrearGestor();

        var ronda = gestor.ObtenerPartidaPorUsuario(999);

        Assert.Null(ronda);
    }

    [Fact]
    public void FinalizarPartida_PartidaExistente_LimpiaTodosLosDiccionarios()
    {
        var gestor = CrearGestor();
        gestor.IniciarPartida(canalId: 1, jugador1Id: 10, jugador2Id: 20, puntosObjetivo: 15);
        gestor.ApuestasActivas[1] = 500;

        gestor.FinalizarPartida(1);

        Assert.False(gestor.PartidasActivas.ContainsKey(1));
        Assert.False(gestor.JugadoresActivos.ContainsKey(10));
        Assert.False(gestor.JugadoresActivos.ContainsKey(20));
        Assert.False(gestor.ApuestasActivas.ContainsKey(1));
    }

    [Fact]
    public void FinalizarPartida_CanalSinPartida_NoTiraExcepcion()
    {
        var gestor = CrearGestor();

        var excepcion = Record.Exception(() => gestor.FinalizarPartida(999));

        Assert.Null(excepcion);
    }

    [Fact]
    public void RegistrarReto_SinRetoPrevio_LoRegistraYDevuelveTrue()
    {
        var gestor = CrearGestor();
        var reto = new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999);

        var resultado = gestor.RegistrarReto(reto);

        Assert.True(resultado);
        Assert.True(gestor.TieneRetoPendiente(10));
    }

    [Fact]
    public void RegistrarReto_RetadorYaTieneRetoPendiente_NoLoRegistraYDevuelveFalse()
    {
        var gestor = CrearGestor();
        gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999));

        var resultado = gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 30, apuesta: 50, puntos: 10, canalId: 2, mensajeId: 998));

        Assert.False(resultado);
        Assert.Equal(20UL, gestor.RetosPendientes[10].RetadoId);
    }

    [Fact]
    public void TryQuitarReto_RetoExistente_LoRemueveYDevuelveTrue()
    {
        var gestor = CrearGestor();
        gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999));

        var resultado = gestor.TryQuitarReto(10, out var reto);

        Assert.True(resultado);
        Assert.NotNull(reto);
        Assert.Equal(20UL, reto!.RetadoId);
        Assert.False(gestor.TieneRetoPendiente(10));
    }

    [Fact]
    public void TryQuitarReto_SinRetoPendiente_DevuelveFalse()
    {
        var gestor = CrearGestor();

        var resultado = gestor.TryQuitarReto(999, out var reto);

        Assert.False(resultado);
        Assert.Null(reto);
    }

    [Fact]
    public void TryQuitarReto_DespuesDeQuitarSePuedeRegistrarUnoNuevo()
    {
        var gestor = CrearGestor();
        gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999));
        gestor.TryQuitarReto(10, out _);

        var resultado = gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 30, apuesta: 50, puntos: 10, canalId: 2, mensajeId: 998));

        Assert.True(resultado);
        Assert.Equal(30UL, gestor.RetosPendientes[10].RetadoId);
    }

    [Fact]
    public async Task RegistrarReto_LimiteRetoVencido_LoQuitaAutomaticamenteDeLosPendientes()
    {
        var gestor = CrearGestor(limiteReto: TimeSpan.FromMilliseconds(50));
        gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999));

        await Task.Delay(TimeSpan.FromMilliseconds(300));

        Assert.False(gestor.TieneRetoPendiente(10));
    }
}
