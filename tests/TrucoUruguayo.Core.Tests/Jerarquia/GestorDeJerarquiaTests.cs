using TrucoUruguayo.Core.Jerarquia;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Jerarquia;

public class GestorDeJerarquiaTests
{
    [Fact]
    public void EsPieza_CartaDelPaloYNumeroDeMuestra_EsPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var dosDeOro = new Carta(2, Palo.Oro);

        Assert.True(gestor.EsPieza(dosDeOro));
        Assert.Equal(Pieza.Dos, gestor.ObtenerPieza(dosDeOro));
    }

    [Fact]
    public void EsPieza_MismoNumeroOtroPalo_NoEsPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var dosDeEspada = new Carta(2, Palo.Espada);

        Assert.False(gestor.EsPieza(dosDeEspada));
        Assert.Null(gestor.ObtenerPieza(dosDeEspada));
    }

    [Fact]
    public void EsPieza_NumeroFueraDeLasCincoPiezas_NoEsPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var sieteDeOro = new Carta(7, Palo.Oro);

        Assert.False(gestor.EsPieza(sieteDeOro));
    }

    [Fact]
    public void ObtenerPieza_MuestraEsPieza_El12DelPaloReemplazaLaPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(4, Palo.Oro)); // la muestra misma es la pieza "Cuatro"

        var doceDeOro = new Carta(12, Palo.Oro);

        Assert.True(gestor.EsPieza(doceDeOro));
        Assert.Equal(Pieza.Cuatro, gestor.ObtenerPieza(doceDeOro));
    }

    [Fact]
    public void ObtenerPieza_MuestraNoEsPieza_El12DelPaloNoEsPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro)); // 3 no es rango de pieza

        var doceDeOro = new Carta(12, Palo.Oro);

        Assert.False(gestor.EsPieza(doceDeOro));
    }

    [Theory]
    [InlineData(2, 30)]
    [InlineData(4, 29)]
    [InlineData(5, 28)]
    [InlineData(11, 27)]
    [InlineData(10, 27)]
    public void ValorEnvido_Piezas_DevuelveValorFijo(int numero, int envidoEsperado)
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var pieza = new Carta(numero, Palo.Oro);

        Assert.Equal(envidoEsperado, gestor.ValorEnvido(pieza));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void ValorEnvido_FigurasNegras_ValenCero(int numero)
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var negra = new Carta(numero, Palo.Espada); // distinto palo que la muestra

        Assert.Equal(0, gestor.ValorEnvido(negra));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    public void ValorEnvido_CartasComunes_ValenSuNumero(int numero)
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var comun = new Carta(numero, Palo.Copa);

        Assert.Equal(numero, gestor.ValorEnvido(comun));
    }

    [Fact]
    public void ValorEnvido_DocePromovido_HeredaElValorDeLaPiezaQueReemplaza()
    {
        var gestor = new GestorDeJerarquia(new Carta(4, Palo.Oro)); // pieza Cuatro

        var doceDeOro = new Carta(12, Palo.Oro);

        Assert.Equal(29, gestor.ValorEnvido(doceDeOro));
    }
}
