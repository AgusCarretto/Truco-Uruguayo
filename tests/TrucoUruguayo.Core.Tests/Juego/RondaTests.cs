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
            var ronda = new Ronda(1UL, 2UL);

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
        var ronda = new Ronda(1UL, 2UL);

        Assert.Equal(EstadoRonda.EsperandoEnvido, ronda.Estado);
    }

    [Fact]
    public void Constructor_TurnoActualArrancaEnJugador1()
    {
        var ronda = new Ronda(42UL, 99UL);

        Assert.Equal(42UL, ronda.TurnoActual);
    }

    [Fact]
    public void Constructor_ArmaElGestorConLaMuestraDeEstaRonda()
    {
        for (var i = 0; i < 50; i++)
        {
            var ronda = new Ronda(1UL, 2UL);

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
            new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Espada), new Carta(6, Palo.Espada) },
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) });

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
            new[] { new Carta(4, Palo.Copa), new Carta(5, Palo.Copa), new Carta(6, Palo.Basto) });

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(4, Palo.Copa));

        Assert.Equal(FaseRonda.SegundaMano, ronda.Fase);
        Assert.Equal(Jugador1, ronda.TurnoActual);

        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(5, Palo.Copa));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
        Assert.Single(ronda.ManoJugador1);
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
            new[] { new Carta(7, Palo.Basto), new Carta(3, Palo.Basto), new Carta(11, Palo.Basto) });

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
            new[] { new Carta(4, Palo.Copa), new Carta(1, Palo.Copa), new Carta(5, Palo.Basto) });

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
            new[] { new Carta(1, Palo.Copa), new Carta(7, Palo.Basto), new Carta(6, Palo.Basto) });

        ronda.JugarCarta(Jugador1, new Carta(1, Palo.Oro));
        ronda.JugarCarta(Jugador2, new Carta(1, Palo.Copa));

        ronda.JugarCarta(Jugador1, new Carta(7, Palo.Copa));
        ronda.JugarCarta(Jugador2, new Carta(7, Palo.Basto));

        ronda.JugarCarta(Jugador1, new Carta(6, Palo.Espada));
        ronda.JugarCarta(Jugador2, new Carta(6, Palo.Basto));

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
    }

    // --- Cantos (Envido) ---

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
        var ronda = new Ronda(Jugador1, Jugador2);

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
        var ronda = new Ronda(Jugador1, Jugador2);

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
        var ronda = new Ronda(Jugador1, Jugador2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.IrseAlMazo(Jugador1);

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador2, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
        Assert.Throws<InvalidOperationException>(() => ronda.IrseAlMazo(Jugador2));
    }

    private static Ronda NuevaRondaConMazoFijo(IEnumerable<Carta> manoJugador1, IEnumerable<Carta> manoJugador2) =>
        new(Jugador1, Jugador2, Muestra, manoJugador1.ToList(), manoJugador2.ToList());
}
