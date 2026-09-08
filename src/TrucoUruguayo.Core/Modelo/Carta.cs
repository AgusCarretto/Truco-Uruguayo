using System;
using System.Linq;

namespace TrucoUruguayo.Core.Modelo;

public class Carta
{
    private static readonly int[] NumerosValidos = { 1, 2, 3, 4, 5, 6, 7, 10, 11, 12 };

    public int Numero { get; }
    public Palo Palo { get; }

    public Carta(int numero, Palo palo)
    {
        if (!NumerosValidos.Contains(numero))
        {
            throw new ArgumentOutOfRangeException(
                nameof(numero), numero, "El numero debe estar entre 1 y 12, salteando 8 y 9.");
        }

        Numero = numero;
        Palo = palo;
    }

    public override bool Equals(object? obj)
    {
        return obj is Carta otra && Numero == otra.Numero && Palo == otra.Palo;
    }

    public override int GetHashCode() => HashCode.Combine(Numero, Palo);

    public override string ToString() => $"{Numero} de {Palo}";
}
