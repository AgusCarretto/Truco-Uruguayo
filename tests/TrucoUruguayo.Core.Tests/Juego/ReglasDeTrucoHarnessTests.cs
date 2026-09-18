using TrucoUruguayo.Core.Jerarquia;
using TrucoUruguayo.Core.Juego;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Juego;

// Harness para chequear reglas de Truco contra el codigo real sin tener que armar una Ronda
// a mano cada vez. Agrega una fila a CasosDeEnvido o CasosDeFlor con la mano, la muestra y
// el valor real (el que corresponda segun las reglas), corré el archivo
// (dotnet test --filter ReglasDeTrucoHarnessTests) y si el codigo da otro numero el test
// falla mostrando la descripcion, lo esperado y lo que dio.
public class ReglasDeTrucoHarnessTests
{
    private const ulong Jugador1 = 1UL;
    private const ulong Jugador2 = 2UL;

    // Mano rival de relleno: sin piezas ni flor, no afecta el calculo de Jugador1 en los
    // casos de Flor (que necesitan una Ronda completa, no solo un GestorDeJerarquia).
    private static readonly List<Carta> ManoRivalDeRelleno =
        [new Carta(3, Palo.Espada), new Carta(6, Palo.Copa), new Carta(1, Palo.Basto)];

    public static IEnumerable<object[]> CasosDeEnvido()
    {
        // descripcion, muestra, mano de Jugador1 (3 cartas), envido esperado.
        yield return new object[]
        {
            "Pieza del 2 de la muestra (30) + un 7 de otro palo (7)",
            new Carta(2, Palo.Oro),
            new Carta[] { new(2, Palo.Oro), new(7, Palo.Copa), new(3, Palo.Basto) },
            37,
        };
    }

    [Theory]
    [MemberData(nameof(CasosDeEnvido))]
    public void MejorEnvido_CasoDeReglas(string descripcion, Carta muestra, Carta[] manoJugador1, int envidoEsperado)
    {
        var gestor = new GestorDeJerarquia(muestra);
        var envidoReal = gestor.MejorEnvido(manoJugador1);

        Assert.True(
            envidoEsperado == envidoReal,
            $"{descripcion}: se esperaba {envidoEsperado}, el codigo dio {envidoReal}.");
    }

    public static IEnumerable<object[]> CasosDeFlor()
    {
        // descripcion, muestra, mano de Jugador1 (3 cartas), tiene flor esperado, puntos de
        // flor esperados (null si no tiene flor / no hace falta chequear el numero).
        yield return new object[]
        {
            "Flor sin piezas, mismo palo (1+3+6=10, +20)",
            new Carta(6, Palo.Copa),
            new Carta[] { new(1, Palo.Espada), new(3, Palo.Espada), new(6, Palo.Espada) },
            true,
            30,
        };
    }

    [Theory]
    [MemberData(nameof(CasosDeFlor))]
    public void Flor_CasoDeReglas(string descripcion, Carta muestra, Carta[] manoJugador1, bool tieneFlorEsperado, int? puntosFlorEsperados)
    {
        var ronda = new Ronda(Jugador1, Jugador2, muestra, manoJugador1.ToList(), ManoRivalDeRelleno, puntosObjetivo: 30, jugadorManoId: Jugador1);

        Assert.True(
            tieneFlorEsperado == ronda.TieneFlor(Jugador1),
            $"{descripcion}: TieneFlor se esperaba {tieneFlorEsperado}, el codigo dio {ronda.TieneFlor(Jugador1)}.");

        if (puntosFlorEsperados is not null)
        {
            var puntosReales = ronda.CalcularPuntosFlor(Jugador1);
            Assert.True(
                puntosFlorEsperados == puntosReales,
                $"{descripcion}: puntos de Flor se esperaban {puntosFlorEsperados}, el codigo dio {puntosReales}.");
        }
    }
}
