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
    private static GestorPartidas CrearGestor() =>
        new(new DiscordSocketClient(), new UsuarioRepository("Host=localhost"));


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
}
