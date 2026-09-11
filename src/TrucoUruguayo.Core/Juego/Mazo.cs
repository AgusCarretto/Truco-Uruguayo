using System;
using System.Collections.Generic;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Juego;

public class Mazo
{
    private static readonly int[] NumerosValidos = { 1, 2, 3, 4, 5, 6, 7, 10, 11, 12 };

    private readonly Random _random;
    private List<Carta> _cartas;

    public Mazo()
    {
        _random = new Random();
        _cartas = GenerarMazoCompleto();
    }

    public void Mezclar()
    {
        _cartas = GenerarMazoCompleto();

        for (var i = _cartas.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_cartas[i], _cartas[j]) = (_cartas[j], _cartas[i]);
        }
    }

    public Reparto Repartir()
    {
        var muestra = _cartas[0];
        var manoJugador1 = _cartas.GetRange(1, 3);
        var manoJugador2 = _cartas.GetRange(4, 3);

        return new Reparto(muestra, manoJugador1, manoJugador2);
    }

    private static List<Carta> GenerarMazoCompleto()
    {
        var cartas = new List<Carta>();

        foreach (Palo palo in Enum.GetValues<Palo>())
        {
            foreach (var numero in NumerosValidos)
            {
                cartas.Add(new Carta(numero, palo));
            }
        }

        return cartas;
    }
}
