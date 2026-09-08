using System;
using System.Linq;

namespace TrucoUruguayo.Core.Modelo;

public sealed class Carta : IEquatable<Carta>
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

    public bool Equals(Carta? otra) => otra is not null && Numero == otra.Numero && Palo == otra.Palo;

    public override bool Equals(object? obj) => Equals(obj as Carta);

    public override int GetHashCode() => HashCode.Combine(Numero, Palo);

    public static bool operator ==(Carta? a, Carta? b) => a is null ? b is null : a.Equals(b);

    public static bool operator !=(Carta? a, Carta? b) => !(a == b);

    public override string ToString() => $"{Numero} de {Palo}";
}
