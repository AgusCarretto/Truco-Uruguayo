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
            "Maximo (37): pieza del 2 de la muestra (30) + un 7 de otro palo (7)",
            new Carta(2, Palo.Oro),
            new Carta[] { new(2, Palo.Oro), new(7, Palo.Copa), new(3, Palo.Basto) },
            37,
        };

        yield return new object[]
        {
            "1 pieza (4) + la carta con mas puntaje de las otras dos: 29 + 4 = 33",
            new Carta(4, Palo.Oro),
            new Carta[] { new(4, Palo.Oro), new(4, Palo.Copa), new(10, Palo.Espada) },
            33,
        };

        yield return new object[]
        {
            "0 piezas, 2 cartas mismo palo (3 y 6): 20 + 3 + 6 = 29 (la 3ra queda afuera)",
            new Carta(6, Palo.Basto),
            new Carta[] { new(3, Palo.Espada), new(6, Palo.Espada), new(10, Palo.Oro) },
            29,
        };

        yield return new object[]
        {
            "0 piezas, sin par (3 palos distintos): se toma la carta mas alta sola (6)",
            new Carta(6, Palo.Basto),
            new Carta[] { new(3, Palo.Espada), new(6, Palo.Copa), new(10, Palo.Oro) },
            6,
        };

        yield return new object[]
        {
            "Minimo (0): 3 figuras de 3 palos distintos, ninguna es pieza",
            new Carta(6, Palo.Copa),
            new Carta[] { new(12, Palo.Espada), new(11, Palo.Basto), new(10, Palo.Oro) },
            0,
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

        yield return new object[]
        {
            "0 piezas, mismo palo, minimo (20): 12+11+10 de Oro con muestra de otro palo",
            new Carta(6, Palo.Espada),
            new Carta[] { new(12, Palo.Oro), new(11, Palo.Oro), new(10, Palo.Oro) },
            true,
            20,
        };

        yield return new object[]
        {
            "1 pieza (4=29) + 2 cartas de OTRO palo entre si (3 y 12): 29 + 3 + 0 = 32",
            new Carta(4, Palo.Oro),
            new Carta[] { new(4, Palo.Oro), new(3, Palo.Espada), new(12, Palo.Espada) },
            true,
            32,
        };

        // OJO: con la formula que diste (pieza mas alta entera + unidad de las otras) esto da
        // 30 + 9 + 8 = 47, no 46. Lo dejo en 47 (lo que da tu propia formula); avisame si en
        // realidad el techo real es otro y hay que capar el caso de 3 piezas en mano.
        yield return new object[]
        {
            "3 piezas en mano (2, 4 y 5 de la muestra): 30 + (29%10=9) + (28%10=8) = 47",
            new Carta(6, Palo.Oro),
            new Carta[] { new(2, Palo.Oro), new(4, Palo.Oro), new(5, Palo.Oro) },
            true,
            47,
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


// REGLA DE ORO (JERARQUÍA): FLOR > ENVIDO > TRUCO > JUGAR CARTA.

// Implementa un validador central en Ronda.cs que evalúe CADA acción del jugador contra estas reglas, en este orden exacto:

// REGLA 1: LA OBLIGACIÓN DE LA FLOR (Bloqueo personal)

// Como se forma: 3 piezas o 2 piezas y una cualquiera o 1 pieza y las otras 2 del mismo palo o 3 cartas del mismo palo.

// Condición: Si un jugador TIENE Flor en su mano (calculado internamente) y AÚN NO LA CANTÓ.

// Efecto: El sistema le debe DENEGAR cualquier intento de cantar Envido, cantar Truco o Jugar una Carta.

// Mensaje/Excepción: "¡Tenés Flor! Estás obligado a cantarla antes de hacer cualquier otra cosa."

// Como se calcula: La Flor es la suma de las 3 cartas. PERO, si tengo mas de 1 pieza, se toma el valor de la mas alta (entre el 5 y el 10 se toma el 5 con 28 puntos) y la segunda pieza o en caso de tres piezas las otras dos se toma la unidad de su valor. (Si tengo 2 4 y 5 30 + 9(de sus 29) + 8(de sus 28))
// En caso de tener 1 pieza y 2 cartas del mismo palo, se toma el valor de la pieza y la suma de las otras dos cartas 4 pieza (29pts) + 3 + 12 (es negra 0pts) = puntaje de flor 32.).
// En caso de tener 3 cartas del mismo palo, se toma la suma de las 3 cartas + 20. 3 + 10 (negra 0pts) + 6 = 29pts
// Puntos maximos: 46. 2, 4 y 5 de la muestra. 30 + 9 + 8
// Puntos minimos: 20. 3 cartas negras mimso palo. 12 11 10 de oro cuando la muestra es otro palo.

// REGLA 2: LA FLOR MATA AL ENVIDO (Bloqueo global)

// Condición: Si cualquier jugador ya cantó Flor (o la cantan como respuesta a un Envido).

// Efecto: El Envido queda ANULADO y permanentemente bloqueado para esa mano. No se puede iniciar un Envido nuevo, y si había un Envido "en el aire" esperando respuesta, muere inmediatamente (no se suman sus puntos).

// Transición: El estado pasa a RespondiendoFlor (si el rival también tiene) o directo a JugandoCartas / EsperandoTruco.

// REGLA 3: LA VENTANA DEL ENVIDO (Caducidad)

//Como se calcula: Teniendo 1 pieza: valor de pieza mas la carta con mas puntaje (imposible otra pieza ya que seria flor). Ejemplo: 4 pieza, 4 y 10(negra 0pts) = 29+4+0= 33
// Teniendo 0 piezas: Si 2 cartas mismo palo: 20 + valor de esas dos cartas (la 3era queda afuera). Ejemplo: 3 y 6 mismo palo, 10 de otro palo(y no es pieza) = 20 + 3 + 6 = 29)
// Teniendo 0 piezas: Si 3 cartas distinto palo se agarra el valor SOLAMENTE de la carta con mas valor: Ejemplo: 3, 6 y 10 distinto palo = 6. valor de envido.
//Puntaje maximo: 37. 30 + 7. 2 de la muestra y 7 de otro palo.
//Puntaje minimo: 0. 3 negras de distinto palo.

// Condición: El Envido (y sus variantes) SOLO se puede cantar si el jugador que lo intenta NO ha jugado ninguna carta aún en esa mano.

// Efecto: Si ambos jugadores ya tiraron su primera carta (o el jugador actual ya tiene 1 carta en la mesa), el menú/botón de Envido debe ser rechazado.

// Aclaración de Turno: Si el Jugador 1 (mano) tira una carta en silencio, el Jugador 2 AÚN PUEDE cantar Envido en su turno antes de tirar su primera carta.

// REGLA 4: LA INDEPENDENCIA DEL TRUCO

// Condición: El Truco se puede cantar en cualquier momento del juego, siempre y cuando sea tu turno, NO haya un Envido pendiente de respuesta, y NO haya una Flor pendiente de respuesta.

// Efecto: El Truco pausa la tirada de cartas hasta que se responda (Quiero / No Quiero / Retruco).