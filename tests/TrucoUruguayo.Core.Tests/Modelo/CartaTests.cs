using System;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Modelo;

public class CartaTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(12)]
    public void Constructor_AceptaNumerosValidos(int numero)
    {
        var carta = new Carta(numero, Palo.Oro);

        Assert.Equal(numero, carta.Numero);
        Assert.Equal(Palo.Oro, carta.Palo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(13)]
    public void Constructor_RechazaNumerosInvalidos(int numero)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Carta(numero, Palo.Oro));
    }

    [Fact]
    public void Equals_ComparaPorValor()
    {
        var a = new Carta(4, Palo.Espada);
        var b = new Carta(4, Palo.Espada);
        var c = new Carta(4, Palo.Basto);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
