using System;
using System.Collections.Generic;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Jerarquia;

public class GestorDeJerarquia
{
    private static readonly Dictionary<int, Pieza> NumeroAPieza = new()
    {
        [2] = Pieza.Dos,
        [4] = Pieza.Cuatro,
        [5] = Pieza.Cinco,
        [11] = Pieza.Caballo,
        [10] = Pieza.Sota,
    };

    private static readonly Dictionary<Pieza, int> ValorEnvidoPorPieza = new()
    {
        [Pieza.Dos] = 30,
        [Pieza.Cuatro] = 29,
        [Pieza.Cinco] = 28,
        [Pieza.Caballo] = 27,
        [Pieza.Sota] = 27,
    };

    private static readonly Dictionary<Pieza, int> ValorTrucoPorPieza = new()
    {
        [Pieza.Dos] = 19,
        [Pieza.Cuatro] = 18,
        [Pieza.Cinco] = 17,
        [Pieza.Caballo] = 16,
        [Pieza.Sota] = 15,
    };

    private readonly Carta _muestra;
    private readonly Pieza? _piezaPromovida;

    public GestorDeJerarquia(Carta muestra)
    {
        _muestra = muestra ?? throw new ArgumentNullException(nameof(muestra));
        _piezaPromovida = NumeroAPieza.TryGetValue(muestra.Numero, out var pieza) ? pieza : (Pieza?)null;
    }

    public bool EsPieza(Carta carta) => ObtenerPieza(carta) != null;

    public Pieza? ObtenerPieza(Carta carta)
    {
        if (carta.Palo != _muestra.Palo)
        {
            return null;
        }

        if (carta.Numero == 12 && _piezaPromovida != null)
        {
            return _piezaPromovida;
        }

        return NumeroAPieza.TryGetValue(carta.Numero, out var pieza) ? pieza : (Pieza?)null;
    }

    public int ValorEnvido(Carta carta)
    {
        var pieza = ObtenerPieza(carta);
        if (pieza != null)
        {
            return ValorEnvidoPorPieza[pieza.Value];
        }

        if (carta.Numero is 10 or 11 or 12)
        {
            return 0;
        }

        return carta.Numero;
    }

    public int ValorTruco(Carta carta)
    {
        var pieza = ObtenerPieza(carta);
        if (pieza != null)
        {
            return ValorTrucoPorPieza[pieza.Value];
        }

        return (carta.Numero, carta.Palo) switch
        {
            (1, Palo.Espada) => 14,
            (1, Palo.Basto) => 13,
            (7, Palo.Espada) => 12,
            (7, Palo.Oro) => 11,
            (3, _) => 10,
            (2, _) => 9,
            (1, _) => 8,
            (12, _) => 7,
            (11, _) => 6,
            (10, _) => 5,
            (7, _) => 4,
            (6, _) => 3,
            (5, _) => 2,
            (4, _) => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(carta), carta, "Numero de carta invalido."),
        };
    }

    public int Comparar(Carta a, Carta b) => ValorTruco(a).CompareTo(ValorTruco(b));
}
