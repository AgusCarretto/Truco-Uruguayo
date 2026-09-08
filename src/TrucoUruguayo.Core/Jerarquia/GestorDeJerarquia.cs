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
}
