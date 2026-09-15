using System;
using System.Collections.Generic;
using System.Linq;
using TrucoUruguayo.Core.Juego;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Juego;

public class RondaTests
{
    private const ulong Jugador1 = 1UL;
    private const ulong Jugador2 = 2UL;
    private static readonly Carta Muestra = new(3, Palo.Oro);

    [Fact]
    public void Constructor_NingunJugadorRecibeLaMuestraYAmbosTienenTresCartas()
    {
        for (var i = 0; i < 50; i++)
        {
            var ronda = new Ronda(1UL, 2UL, 15);

            Assert.Equal(3, ronda.ManoJugador1.Count);
            Assert.Equal(3, ronda.ManoJugador2.Count);
            Assert.DoesNotContain(ronda.Muestra, ronda.ManoJugador1);
            Assert.DoesNotContain(ronda.Muestra, ronda.ManoJugador2);
            Assert.Empty(ronda.ManoJugador1.Intersect(ronda.ManoJugador2));
        }
    }

    [Fact]
    public void Constructor_EstadoArrancaEnEsperandoEnvido()
    {
        var ronda = new Ronda(1UL, 2UL, 15);

        Assert.Equal(EstadoRonda.EsperandoEnvido, ronda.Estado);
    }

    [Fact]
    public void Constructor_TurnoActualArrancaIgualAlJugadorMano()
    {
        var ronda = new Ronda(42UL, 99UL, 15);

        Assert.True(ronda.JugadorManoId == 42UL || ronda.JugadorManoId == 99UL);
        Assert.Equal(ronda.JugadorManoId, ronda.TurnoActual);
    }

    [Fact]
    public void Constructor_JugadorManoEsAleatorioEntreAmbosJugadores()
    {
        var vecesJugador1 = 0;
        var vecesJugador2 = 0;

        for (var i = 0; i < 100; i++)
        {
            var ronda = new Ronda(Jugador1, Jugador2, 15);
            if (ronda.JugadorManoId == Jugador1)
            {
                vecesJugador1++;
            }
            else
            {
                vecesJugador2++;
            }
        }

        Assert.True(vecesJugador1 > 0);
        Assert.True(vecesJugador2 > 0);
    }

    [Fact]
    public void Constructor_ArmaElGestorConLaMuestraDeEstaRonda()
    {
        for (var i = 0; i < 50; i++)
        {
            var ronda = new Ronda(1UL, 2UL, 15);

            var dosDelPaloDeLaMuestra = new Carta(2, ronda.Muestra.Palo);

            Assert.Equal(Pieza.Dos, ronda.Gestor.ObtenerPieza(dosDelPaloDeLaMuestra));

            var doceDelPaloDeLaMuestra = new Carta(12, ronda.Muestra.Palo);
            var piezaEsperadaParaElDoce = ronda.Muestra.Numero switch
            {
                2 => Pieza.Dos,
                4 => Pieza.Cuatro,
                5 => Pieza.Cinco,
                11 => Pieza.Caballo,
                10 => Pieza.Sota,
                _ => (Pieza?)null,
            };

            Assert.Equal(piezaEsperadaParaElDoce, ronda.Gestor.ObtenerPieza(doceDelPaloDeLaMuestra));
        }
    }

    // --- Validacion de turnos ---

    [Fact]
    public void JugarCarta_NoEsSuTurno_Arroja()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(4, Palo.Copa), new Carta(6, Palo.Espada) },
            new[] { new Carta(4, Palo.Oro), new Carta(1, Palo.Copa), new Carta(6, Palo.Basto) });

        Assert.Throws<InvalidOperationException>(() => ronda.JugarCarta(Jugador2, new Carta(4, Palo.Oro)));
    }

    [Fact]
    public void JugarCarta_JugadorNoTieneEsaCarta_Arroja()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(4, Palo.Copa), new Carta(6, Palo.Espada) },
            new[] { new Carta(4, Palo.Oro), new Carta(1, Palo.Copa), new Carta(6, Palo.Basto) });

        Assert.Throws<ArgumentException>(() => ronda.JugarCarta(Jugador1, new Carta(5, Palo.Espada)));
    }

    [Fact]
    public void JugarCarta_TrasJugarPasaElTurnoAlOponente()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(4, Palo.Copa), new Carta(6, Palo.Espada) },
            new[] { new Carta(4, Palo.Oro), new Carta(1, Palo.Copa), new Carta(6, Palo.Basto) });

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));

        Assert.Equal(Jugador2, ronda.TurnoActual);
    }

    [Fact]
    public void JugarCarta_RondaYaFinalizada_Arroja()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(6, Palo.Basto) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 1);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Throws<InvalidOperationException>(() => ronda.JugarCarta(Jugador1, new Carta(6, Palo.Espada)));
    }

    // --- Ganar dos manos ---

    [Fact]
    public void JugarCarta_GanaDosManosSeguidas_TerminaLaRondaSinJugarLaTercera()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 1);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));

        Assert.Equal(FaseRonda.SegundaMano, ronda.Fase);
        Assert.Equal(Jugador1, ronda.TurnoActual);

        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
        Assert.Single(ronda.ManoJugador1);
        Assert.Equal(1, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
    }

    [Fact]
    public void JugarCarta_GanaLaRondaConTrucoQuerido_SumaElValorDeTrucoActualAlGanador()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
    }

    [Fact]
    public void JugarCarta_GanaLaRondaConRetrucoQuerido_SumaTresPuntosAlGanador()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 3);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);
        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
        ronda.ResponderTruco(Jugador1, RespuestaCanto.Quiero);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
        Assert.Equal(3, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
    }

    [Fact]
    public void JugarCarta_GanaLaRondaConValeCuatroQuerido_SumaCuatroPuntosAlGanador()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 4);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);
        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
        ronda.ResponderTruco(Jugador1, RespuestaCanto.Quiero);
        ronda.GritarTruco(Jugador1, CantoTruco.ValeCuatro);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
        Assert.Equal(4, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
    }

    [Fact]
    public void JugarCarta_GanarLaRondaPorManosAlcanzaElPuntosObjetivo_CortaLaPartidaConEseGanador()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador1);
    }

    [Fact]
    public void JugarCarta_GanarLaManoSinAlcanzarElObjetivo_IniciaLaSiguienteManoAutomaticamente()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 15);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        // Jugador1 gano la mano (2/3 bazas) pero el objetivo es 15: no corta la partida,
        // arranca una mano nueva automaticamente con cartas y muestra frescas.
        Assert.Equal(FaseRonda.PrimeraMano, ronda.Fase);
        Assert.Null(ronda.GanadorRonda);
        Assert.Equal(1, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
        Assert.Equal(3, ronda.ManoJugador1.Count);
        Assert.Equal(3, ronda.ManoJugador2.Count);
        Assert.Empty(ronda.CartasJugadasJugador1);
        Assert.Empty(ronda.CartasJugadasJugador2);
        Assert.Equal(1, ronda.ValorTrucoActual);
        Assert.Null(ronda.TurnoCantoTruco);
        Assert.Equal(EstadoRonda.EsperandoEnvido, ronda.Estado);

        // Jugador1 era mano en la mano anterior: ahora le toca a Jugador2.
        Assert.Equal(Jugador2, ronda.JugadorManoId);
        Assert.Equal(Jugador2, ronda.TurnoActual);
    }

    [Fact]
    public void JugarCarta_ElGanadorDeLaManoLideraLaSiguiente()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(4, Palo.Copa), new Carta(1, Palo.Espada), new Carta(6, Palo.Espada) },
            new[] { new Carta(1, Palo.Oro), new Carta(4, Palo.Oro), new Carta(6, Palo.Basto) });

        ronda.JugarCarta(Jugador1, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador2, new Carta(1, Palo.Oro));

        Assert.Equal(Jugador2, ronda.TurnoActual);
    }

    // --- Victoria por parda ---

    [Fact]
    public void Parda_PrimeraManoEmpatada_GanaLaRondaQuienGanaLaSegunda()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(7, Palo.Copa), new Carta(4, Palo.Copa), new Carta(12, Palo.Basto) },
            new[] { new Carta(7, Palo.Basto), new Carta(3, Palo.Basto), new Carta(11, Palo.Espada) },
            puntosObjetivo: 1);

        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Copa));
        ronda.JugarCarta(Jugador2, new Carta(7, Palo.Basto));

        Assert.Equal(FaseRonda.SegundaMano, ronda.Fase);
        Assert.Equal(Jugador1, ronda.TurnoActual);

        ronda.JugarCarta(Jugador1, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador2, new Carta(3, Palo.Basto));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador2, ronda.GanadorRonda);
    }

    [Fact]
    public void Parda_SegundaManoEmpatada_GanaLaRondaQuienGanoLaPrimera()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(1, Palo.Oro), new Carta(12, Palo.Basto) },
            new[] { new Carta(4, Palo.Copa), new Carta(1, Palo.Copa), new Carta(5, Palo.Basto) },
            puntosObjetivo: 1);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Oro));
        ronda.JugarCarta(Jugador2, new Carta(1, Palo.Copa));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
    }

    [Fact]
    public void Parda_LasTresManosEmpatadas_GanaElJugadorQueEmpezoLaRonda()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Oro), new Carta(7, Palo.Copa), new Carta(6, Palo.Espada) },
            new[] { new Carta(1, Palo.Copa), new Carta(7, Palo.Basto), new Carta(6, Palo.Basto) },
            puntosObjetivo: 1);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Oro));
        ronda.JugarCarta(Jugador2, new Carta(1, Palo.Copa));

        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Copa));
        ronda.JugarCarta(Jugador2, new Carta(7, Palo.Basto));

        ronda.JugarCarta(Jugador1, new Carta(6, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(6, Palo.Basto));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
    }

    // --- Memoria visual de la ultima baza ---

    [Fact]
    public void JugarCarta_GuardaLaUltimaJugadaYElGanadorDeLaBaza()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(4, Palo.Copa), new Carta(6, Palo.Espada) },
            new[] { new Carta(4, Palo.Oro), new Carta(1, Palo.Copa), new Carta(6, Palo.Basto) });

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Oro));

        Assert.Equal(new Carta(1, Palo.Espada), ronda.UltimaCartaMesaJ1);
        Assert.Equal(new Carta(4, Palo.Oro), ronda.UltimaCartaMesaJ2);
        // Muestra = 3 de Oro, asi que el 4 de Oro es la Pieza "4 de la muestra" y le gana
        // hasta al As de Espada.
        Assert.Equal(Jugador2, ronda.GanadorUltimaMano);
    }

    [Fact]
    public void JugarCarta_Parda_GanadorUltimaManoEsNullPeroLasCartasQuedanGuardadas()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(7, Palo.Copa), new Carta(4, Palo.Copa), new Carta(12, Palo.Basto) },
            new[] { new Carta(7, Palo.Basto), new Carta(3, Palo.Basto), new Carta(11, Palo.Espada) },
            puntosObjetivo: 1);

        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Copa));
        ronda.JugarCarta(Jugador2, new Carta(7, Palo.Basto));

        Assert.Null(ronda.GanadorUltimaMano);
        Assert.Equal(new Carta(7, Palo.Copa), ronda.UltimaCartaMesaJ1);
        Assert.Equal(new Carta(7, Palo.Basto), ronda.UltimaCartaMesaJ2);
    }

    [Fact]
    public void JugarCarta_GanarLaRondaYArrancarManoNueva_LasUltimasCartasSiguenDisponiblesAunqueLaMesaSeLimpio()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 15);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        // Se gano la ronda (2/3 bazas) sin llegar al objetivo: ya arranco mano nueva y
        // las listas de cartas jugadas quedaron vacias otra vez...
        Assert.Equal(FaseRonda.PrimeraMano, ronda.Fase);
        Assert.Empty(ronda.CartasJugadasJugador1);
        Assert.Empty(ronda.CartasJugadasJugador2);

        // ...pero la "ultima jugada" sigue mostrando la baza que decidio la ronda anterior,
        // que es justo lo que necesita la UI para no romperse leyendo una lista vacia.
        Assert.Equal(new Carta(7, Palo.Espada), ronda.UltimaCartaMesaJ1);
        Assert.Equal(new Carta(5, Palo.Copa), ronda.UltimaCartaMesaJ2);
        Assert.Equal(Jugador1, ronda.GanadorUltimaMano);
    }

    // --- Cantos (Envido) ---

    [Fact]
    public void CalcularEnvido_DevuelveElMejorEnvidoDeLaManoDelJugador()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(7, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) }, // mejor envido: 7
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) }); // mejor envido: 7+6+20=33

        Assert.Equal(7, ronda.CalcularEnvido(Jugador1));
        Assert.Equal(33, ronda.CalcularEnvido(Jugador2));
    }

    [Fact]
    public void PuedeCantarEnvido_SoloElJugadorEnTurnoPuedeCantar()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(4, Palo.Copa), new Carta(6, Palo.Espada) },
            new[] { new Carta(4, Palo.Oro), new Carta(1, Palo.Copa), new Carta(6, Palo.Basto) });

        // Jugador1 es mano y le toca a el: solo el puede cantar por ahora.
        Assert.True(ronda.PuedeCantarEnvido(Jugador1));
        Assert.False(ronda.PuedeCantarEnvido(Jugador2));

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));

        // Ahora le toca a Jugador2: el puede cantar (no jugo nada todavia). Jugador1 ya
        // no puede: ni es su turno, ni tiene 0 cartas jugadas.
        Assert.False(ronda.PuedeCantarEnvido(Jugador1));
        Assert.True(ronda.PuedeCantarEnvido(Jugador2));
    }

    [Fact]
    public void ResponderEnvido_JugadorQueYaJugoUnaCarta_CalculaElEnvidoConLaManoOriginalDeTresCartas()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) }, // mejor envido: 6
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) }); // mejor envido: 7+6+20=33

        // Jugador1 juega primero: su mano "en juego" queda con 2 cartas.
        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));

        // Jugador2 igual puede cantar (todavia no jugo nada), y Jugador1 tiene que poder
        // responder sin que MejorEnvido reviente por tener menos de 3 cartas "en mano".
        ronda.CantarEnvido(Jugador2, Canto.Envido);

        var excepcion = Record.Exception(() => ronda.ResponderEnvido(Jugador1, RespuestaCanto.Quiero));

        Assert.Null(excepcion);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
    }

    [Fact]
    public void CantarEnvido_DespuesDeQueElRivalYaJugoUnaCarta_IgualmentePuedeCantar()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(4, Palo.Copa), new Carta(6, Palo.Espada) },
            new[] { new Carta(4, Palo.Oro), new Carta(1, Palo.Copa), new Carta(6, Palo.Basto) });

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));

        // Antes de este fix esto tiraba, porque Estado ya habia pasado a JugandoCartas
        // apenas Jugador1 jugo su carta (aunque Jugador2 todavia no jugo la suya).
        ronda.CantarEnvido(Jugador2, Canto.Envido);

        Assert.Equal(EstadoRonda.RespondiendoCanto, ronda.Estado);
        Assert.Equal(Jugador2, ronda.JugadorQueCanto);
    }

    [Fact]
    public void CantarEnvido_UnaVezResuelto_NoSePuedeVolverACantarAunqueNadieHayaJugadoTodavia()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(7, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        ronda.CantarEnvido(Jugador1, Canto.Envido);
        ronda.ResponderEnvido(Jugador2, RespuestaCanto.Quiero);

        Assert.True(ronda.EnvidoCantado);
        Assert.False(ronda.PuedeCantarEnvido(Jugador1));
        Assert.False(ronda.PuedeCantarEnvido(Jugador2));
        Assert.Throws<InvalidOperationException>(() => ronda.CantarEnvido(Jugador1, Canto.RealEnvido));
    }

    [Fact]
    public void JugarCarta_GanarLaManoSinAlcanzarElObjetivo_ReseteaEnvidoCantado()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 15);

        ronda.CantarEnvido(Jugador1, Canto.Envido);
        ronda.ResponderEnvido(Jugador2, RespuestaCanto.Quiero);
        Assert.True(ronda.EnvidoCantado);

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));
        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        // Jugador1 gano 2/3 bazas y nadie llego a 15: arranca mano nueva.
        Assert.Equal(FaseRonda.PrimeraMano, ronda.Fase);
        Assert.False(ronda.EnvidoCantado);
        Assert.True(ronda.PuedeCantarEnvido(ronda.TurnoActual));
    }

    [Fact]
    public void CantarEnvido_Quiero_GanaElJugadorConMejorEnvido()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(7, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) }, // mejor envido: 7
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) }); // mejor envido: 7+6+20=33

        ronda.CantarEnvido(Jugador1, Canto.Envido);

        Assert.Equal(EstadoRonda.RespondiendoCanto, ronda.Estado);
        Assert.Equal(Jugador1, ronda.JugadorQueCanto);
        Assert.Equal(Jugador2, ronda.TurnoActual);

        ronda.ResponderEnvido(Jugador2, RespuestaCanto.Quiero);

        Assert.Equal(EstadoRonda.JugandoCartas, ronda.Estado);
        Assert.Equal(Jugador1, ronda.TurnoActual);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
    }

    [Fact]
    public void CantarEnvido_NoQuiero_AsignaElPuntoDeRechazoAQuienCanto()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(7, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        ronda.CantarEnvido(Jugador1, Canto.RealEnvido);
        ronda.ResponderEnvido(Jugador2, RespuestaCanto.NoQuiero);

        Assert.Equal(EstadoRonda.JugandoCartas, ronda.Estado);
        Assert.Equal(Jugador1, ronda.TurnoActual);
        Assert.Equal(1, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
    }

    [Fact]
    public void CantarEnvido_EnvidoActualReflejaElCantoPendienteYSeLimpiaAlResponder()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(7, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        Assert.Null(ronda.EnvidoActual);

        ronda.CantarEnvido(Jugador1, Canto.RealEnvido);

        Assert.Equal(Canto.RealEnvido, ronda.EnvidoActual);

        ronda.ResponderEnvido(Jugador2, RespuestaCanto.Quiero);

        Assert.Null(ronda.EnvidoActual);
    }

    [Fact]
    public void CantarEnvido_FaltaEnvido_Quiero_SumaLoQueLeFaltaAlQueVaGanandoParaElObjetivo()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(12, Palo.Copa) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) },
            puntosObjetivo: 20);

        // Jugador1 se va al mazo antes de jugar ninguna carta: Jugador2 se lleva el
        // ValorTrucoActual base (1) y, como no alcanza el objetivo, arranca mano nueva.
        ronda.IrseAlMazo(Jugador1);

        Assert.Equal(1, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
        Assert.Equal(FaseRonda.PrimeraMano, ronda.Fase);

        var quienCanta = ronda.TurnoActual;
        var quienResponde = quienCanta == Jugador1 ? Jugador2 : Jugador1;
        var puntos1Antes = ronda.PuntosJugador1;
        var puntos2Antes = ronda.PuntosJugador2;

        ronda.CantarEnvido(quienCanta, Canto.FaltaEnvido);

        Assert.Equal(Canto.FaltaEnvido, ronda.EnvidoActual);

        ronda.ResponderEnvido(quienResponde, RespuestaCanto.Quiero);

        // El que va ganando (Jugador2, con 1) necesita 19 para llegar a 20 - sin importar
        // quien gane este envido puntual, eso es lo que tiene que sumarse en total.
        var delta = (ronda.PuntosJugador1 - puntos1Antes) + (ronda.PuntosJugador2 - puntos2Antes);
        Assert.Equal(19, delta);
    }

    [Fact]
    public void JugarCarta_HayUnCantoPendiente_Arroja()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(7, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        ronda.CantarEnvido(Jugador1, Canto.Envido);

        Assert.Throws<InvalidOperationException>(() => ronda.JugarCarta(Jugador2, new Carta(7, Palo.Oro)));
    }

    // --- Cantos (Truco) ---

    [Fact]
    public void GritarTruco_Quiero_LuegoRetruco_SoloElQueRespondioPuedeSubir()
    {
        // Usa el constructor de mazo fijo (Jugador1 mano determinista) porque este test
        // verifica el TurnoActual restaurado tras un "Quiero" explicito, que depende de
        // quien era mano al arrancar — con el constructor publico (mano aleatoria) el
        // resultado esperado cambiaria segun el sorteo.
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(2, Palo.Espada), new Carta(3, Palo.Espada) },
            new[] { new Carta(4, Palo.Espada), new Carta(5, Palo.Espada), new Carta(6, Palo.Espada) });

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);

        Assert.Equal(EstadoRonda.RespondiendoTruco, ronda.Estado);
        Assert.Equal(Jugador1, ronda.JugadorQueGritoTruco);
        Assert.Equal(Jugador2, ronda.TurnoActual);

        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        Assert.Equal(2, ronda.ValorTrucoActual);
        Assert.Equal(Jugador2, ronda.TurnoCantoTruco);
        Assert.Equal(EstadoRonda.EsperandoEnvido, ronda.Estado);
        Assert.Equal(Jugador1, ronda.TurnoActual);

        Assert.Throws<InvalidOperationException>(() => ronda.GritarTruco(Jugador1, CantoTruco.Retruco));

        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);

        Assert.Equal(EstadoRonda.RespondiendoTruco, ronda.Estado);
        Assert.Equal(Jugador2, ronda.JugadorQueGritoTruco);
        Assert.Equal(Jugador1, ronda.TurnoActual);
    }

    [Fact]
    public void ResponderTruco_NoQuieroElRetruco_GanaLaRondaQuienLoCantoConLosPuntosDeTruco()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
        ronda.ResponderTruco(Jugador1, RespuestaCanto.NoQuiero);

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador2, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
    }

    [Fact]
    public void IrseAlMazo_TerminaLaRondaYElRivalSeLlevaElValorDeTrucoActual()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.IrseAlMazo(Jugador1);

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador2, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
        Assert.Throws<InvalidOperationException>(() => ronda.IrseAlMazo(Jugador2));
    }

    // --- AFK / UltimaActividad ---

    [Fact]
    public void Constructor_UltimaActividadArrancaCercaDeAhora()
    {
        var antes = DateTime.UtcNow;
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        var despues = DateTime.UtcNow;

        Assert.InRange(ronda.UltimaActividad, antes, despues);
    }

    [Fact]
    public void JugarCarta_ActualizaUltimaActividad()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        var antes = DateTime.UtcNow;

        var quienJuega = ronda.TurnoActual;
        var mano = quienJuega == Jugador1 ? ronda.ManoJugador1 : ronda.ManoJugador2;
        ronda.JugarCarta(quienJuega, mano[0]);

        Assert.True(ronda.UltimaActividad >= antes);
    }

    [Fact]
    public void CantarEnvido_ActualizaUltimaActividad()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        var antes = DateTime.UtcNow;

        ronda.CantarEnvido(ronda.TurnoActual, Canto.Envido);

        Assert.True(ronda.UltimaActividad >= antes);
    }

    [Fact]
    public void ResponderEnvido_ActualizaUltimaActividad()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        var quienCanta = ronda.TurnoActual;
        ronda.CantarEnvido(quienCanta, Canto.Envido);
        var quienResponde = quienCanta == Jugador1 ? Jugador2 : Jugador1;
        var antes = DateTime.UtcNow;

        ronda.ResponderEnvido(quienResponde, RespuestaCanto.Quiero);

        Assert.True(ronda.UltimaActividad >= antes);
    }

    [Fact]
    public void GritarTruco_ActualizaUltimaActividad()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        var antes = DateTime.UtcNow;

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);

        Assert.True(ronda.UltimaActividad >= antes);
    }

    [Fact]
    public void ResponderTruco_ActualizaUltimaActividad()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        var antes = DateTime.UtcNow;

        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        Assert.True(ronda.UltimaActividad >= antes);
    }

    // --- Escalada de Truco "tipo tenis" ---

    [Fact]
    public void GritarTruco_RivalEscalaDirectoSinDecirQuiero_AceptaElAnteriorYQuedaPendienteElNuevo()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        // Jugador2 es quien debe responder. En vez de "Quiero", escala directo a Retruco.
        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);

        Assert.Equal(2, ronda.ValorTrucoActual); // el Truco quedo aceptado de forma implicita
        Assert.Equal(Jugador2, ronda.TurnoCantoTruco);
        Assert.Equal(EstadoRonda.RespondiendoTruco, ronda.Estado);
        Assert.Equal(Jugador2, ronda.JugadorQueGritoTruco);
        Assert.Equal(CantoTruco.Retruco, ronda.CantoTrucoPendiente);
        Assert.Equal(Jugador1, ronda.TurnoActual); // ahora Jugador1 tiene que responder el Retruco
    }

    [Fact]
    public void GritarTruco_EscaladaTenis_ElQueNoDebeResponderNoPuedeEscalar()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);

        // Jugador1 (quien ya cantó) intenta escalar de nuevo antes de que Jugador2 responda.
        Assert.Throws<InvalidOperationException>(() => ronda.GritarTruco(Jugador1, CantoTruco.Retruco));
    }

    [Fact]
    public void GritarTruco_EscaladaTenis_SaltandoUnNivel_ArrojaYNoCambiaNadaDelEstadoAnterior()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);

        // Jugador2 intenta saltar directo a Vale Cuatro sin pasar por Retruco.
        Assert.Throws<InvalidOperationException>(() => ronda.GritarTruco(Jugador2, CantoTruco.ValeCuatro));

        // El intento invalido no debe haber aceptado el Truco pendiente ni tocado nada.
        Assert.Equal(1, ronda.ValorTrucoActual);
        Assert.Null(ronda.TurnoCantoTruco);
        Assert.Equal(CantoTruco.Truco, ronda.CantoTrucoPendiente);
        Assert.Equal(Jugador1, ronda.JugadorQueGritoTruco);
        Assert.Equal(EstadoRonda.RespondiendoTruco, ronda.Estado);
        Assert.Equal(Jugador2, ronda.TurnoActual);
    }

    [Fact]
    public void GritarTruco_EscaladaTenisHastaValeCuatro_TerminaConValorCuatro()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
        ronda.GritarTruco(Jugador1, CantoTruco.ValeCuatro);

        Assert.Equal(3, ronda.ValorTrucoActual); // Retruco quedo aceptado de forma implicita
        Assert.Equal(CantoTruco.ValeCuatro, ronda.CantoTrucoPendiente);
        Assert.Equal(Jugador1, ronda.JugadorQueGritoTruco);
        Assert.Equal(Jugador2, ronda.TurnoActual);

        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        Assert.Equal(4, ronda.ValorTrucoActual);
    }

    // --- Flor ---

    [Fact]
    public void EsPieza_CartaDelPaloDeLaMuestraConNumeroDePieza_DevuelveTrue()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        Assert.True(ronda.EsPieza(new Carta(2, Palo.Oro)));
        Assert.False(ronda.EsPieza(new Carta(3, Palo.Espada)));
    }

    [Fact]
    public void ValorPieza_CartasDePieza_DevuelveElValorEsperado()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        Assert.Equal(30, ronda.ValorPieza(new Carta(2, Palo.Oro)));
        Assert.Equal(29, ronda.ValorPieza(new Carta(4, Palo.Oro)));
        Assert.Equal(28, ronda.ValorPieza(new Carta(5, Palo.Oro)));
        Assert.Equal(27, ronda.ValorPieza(new Carta(11, Palo.Oro)));
        Assert.Equal(27, ronda.ValorPieza(new Carta(10, Palo.Oro)));
        Assert.Equal(0, ronda.ValorPieza(new Carta(3, Palo.Espada)));
    }

    [Fact]
    public void TieneFlor_TresPiezas_DevuelveTrue()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.True(ronda.TieneFlor(Jugador1));
    }

    [Fact]
    public void TieneFlor_DosPiezas_DevuelveTrue()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(3, Palo.Espada) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.True(ronda.TieneFlor(Jugador1));
    }

    [Fact]
    public void TieneFlor_UnaPiezaYLasOtrasDosMismoPalo_DevuelveTrue()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(6, Palo.Espada), new Carta(7, Palo.Espada) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.True(ronda.TieneFlor(Jugador1));
    }

    [Fact]
    public void TieneFlor_UnaPiezaYLasOtrasDosPalosDistintos_DevuelveFalse()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(6, Palo.Espada), new Carta(7, Palo.Basto) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.False(ronda.TieneFlor(Jugador1));
    }

    [Fact]
    public void TieneFlor_CeroPiezasYLasTresMismoPalo_DevuelveTrue()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(6, Palo.Espada), new Carta(7, Palo.Espada), new Carta(3, Palo.Espada) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.True(ronda.TieneFlor(Jugador1));
    }

    [Fact]
    public void TieneFlor_CeroPiezasYPalosMixtos_DevuelveFalse()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(6, Palo.Espada), new Carta(7, Palo.Basto), new Carta(3, Palo.Copa) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.False(ronda.TieneFlor(Jugador1));
    }

    [Fact]
    public void CalcularPuntosFlor_TresPiezas_SumaLaMasAltaEnteraYElDigitoDeUnidadesDeLasOtrasDos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.Equal(47, ronda.CalcularPuntosFlor(Jugador1));
    }

    [Fact]
    public void CalcularPuntosFlor_DosPiezas_SumaLaMasAltaEnteraElDigitoDeLaOtraYElValorDeLaTercera()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(3, Palo.Espada) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.Equal(42, ronda.CalcularPuntosFlor(Jugador1));
    }

    [Fact]
    public void CalcularPuntosFlor_UnaPieza_SumaLaPiezaMasElValorDeLasOtrasDos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(6, Palo.Espada), new Carta(7, Palo.Espada) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.Equal(43, ronda.CalcularPuntosFlor(Jugador1));
    }

    [Fact]
    public void CalcularPuntosFlor_CeroPiezas_Suma20MasElValorDeLasTres()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(6, Palo.Espada), new Carta(7, Palo.Espada), new Carta(3, Palo.Espada) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        Assert.Equal(36, ronda.CalcularPuntosFlor(Jugador1));
    }

    [Fact]
    public void CantarFlor_ConFlor_MarcaFlorCantadaYSumaTresPuntos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        ronda.CantarFlor(Jugador1);

        Assert.True(ronda.FlorCantada[Jugador1]);
        Assert.Equal(3, ronda.PuntosJugador1);
    }

    [Fact]
    public void CantarFlor_SinFlor_TiraExcepcionYNoSumaPuntos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(6, Palo.Espada), new Carta(7, Palo.Basto), new Carta(3, Palo.Copa) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        var excepcion = Record.Exception(() => ronda.CantarFlor(Jugador1));

        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Equal("No tenés Flor, no seas fantasma.", excepcion.Message);
        Assert.Equal(0, ronda.PuntosJugador1);
    }

    [Fact]
    public void CantarFlor_FueraDeTurno_TiraExcepcion()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) });

        // NuevaRondaConMazoFijo pone a Jugador1 como mano, asi que a Jugador2 todavia no le toca.
        var excepcion = Record.Exception(() => ronda.CantarFlor(Jugador2));

        Assert.IsType<InvalidOperationException>(excepcion);
    }

    [Fact]
    public void CantarEnvido_ConFlorSinCantar_TiraExcepcion()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        var excepcion = Record.Exception(() => ronda.CantarEnvido(Jugador1, Canto.Envido));

        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Equal("¡Tenés Flor! Debés cantarla antes del envido.", excepcion.Message);
    }

    [Fact]
    public void CantarEnvido_DespuesDeCantarFlor_YaNoBloquea()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        ronda.CantarFlor(Jugador1);
        var excepcion = Record.Exception(() => ronda.CantarEnvido(Jugador1, Canto.Envido));

        Assert.Null(excepcion);
    }

    [Fact]
    public void JugarCarta_ConFlorSinCantar_TiraExcepcion()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        var excepcion = Record.Exception(() => ronda.JugarCarta(Jugador1, new Carta(2, Palo.Oro)));

        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Equal("¡Tenés Flor! Debés cantarla antes de jugar una carta.", excepcion.Message);
    }

    [Fact]
    public void JugarCarta_DespuesDeCantarFlor_YaNoBloquea()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        ronda.CantarFlor(Jugador1);
        var excepcion = Record.Exception(() => ronda.JugarCarta(Jugador1, new Carta(2, Palo.Oro)));

        Assert.Null(excepcion);
    }

    private static Ronda NuevaRondaConMazoFijo(IEnumerable<Carta> manoJugador1, IEnumerable<Carta> manoJugador2, int puntosObjetivo = 15) =>
        new(Jugador1, Jugador2, Muestra, manoJugador1.ToList(), manoJugador2.ToList(), puntosObjetivo, jugadorManoId: Jugador1);
}
