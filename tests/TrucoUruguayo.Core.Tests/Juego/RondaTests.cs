using System.Linq;
using TrucoUruguayo.Core.Juego;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Juego;

public class RondaTests
{
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
        }
    }
}
