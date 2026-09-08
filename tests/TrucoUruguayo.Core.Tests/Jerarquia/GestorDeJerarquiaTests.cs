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
}
