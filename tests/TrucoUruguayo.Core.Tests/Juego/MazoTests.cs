using System.Collections.Generic;
using System.Linq;
using TrucoUruguayo.Core.Juego;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Juego;

public class MazoTests
{
    [Fact]
    public void Repartir_LaMuestraNuncaEstaEnNingunaMano()
    {
        for (var i = 0; i < 50; i++)
        {
            var mazo = new Mazo();
            mazo.Mezclar();

            var reparto = mazo.Repartir();

            Assert.DoesNotContain(reparto.Muestra, reparto.ManoJugador1);
            Assert.DoesNotContain(reparto.Muestra, reparto.ManoJugador2);
        }
    }

    [Fact]
    public void Repartir_CadaJugadorRecibeExactamenteTresCartas()
    {
        for (var i = 0; i < 50; i++)
        {
            var mazo = new Mazo();
            mazo.Mezclar();

            var reparto = mazo.Repartir();

            Assert.Equal(3, reparto.ManoJugador1.Count);
            Assert.Equal(3, reparto.ManoJugador2.Count);
        }
    }

    [Fact]
    public void Repartir_LasSieteCartasRepartidasSonTodasDistintas()
    {
        for (var i = 0; i < 50; i++)
        {
            var mazo = new Mazo();
            mazo.Mezclar();

            var reparto = mazo.Repartir();

            var todasLasCartas = new List<Carta> { reparto.Muestra };
            todasLasCartas.AddRange(reparto.ManoJugador1);
            todasLasCartas.AddRange(reparto.ManoJugador2);

            Assert.Equal(7, todasLasCartas.Distinct().Count());
        }
    }
}
