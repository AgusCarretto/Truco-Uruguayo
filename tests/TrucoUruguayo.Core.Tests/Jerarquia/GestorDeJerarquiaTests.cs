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

    [Fact]
    public void ValorTruco_OrdenDeLasPiezas()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var dos = new Carta(2, Palo.Oro);
        var cuatro = new Carta(4, Palo.Oro);
        var cinco = new Carta(5, Palo.Oro);
        var caballo = new Carta(11, Palo.Oro);
        var sota = new Carta(10, Palo.Oro);

        Assert.True(gestor.ValorTruco(dos) > gestor.ValorTruco(cuatro));
        Assert.True(gestor.ValorTruco(cuatro) > gestor.ValorTruco(cinco));
        Assert.True(gestor.ValorTruco(cinco) > gestor.ValorTruco(caballo));
        Assert.True(gestor.ValorTruco(caballo) > gestor.ValorTruco(sota));
    }

    [Fact]
    public void ValorTruco_PiezaSiempreLeGanaACualquierCartaComun()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var sota = new Carta(10, Palo.Oro); // pieza mas baja
        var ancho = new Carta(1, Palo.Espada); // carta comun mas alta

        Assert.True(gestor.ValorTruco(sota) > gestor.ValorTruco(ancho));
    }

    [Fact]
    public void ValorTruco_JerarquiaClasica()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        var anchoEspada = new Carta(1, Palo.Espada);
        var anchoBasto = new Carta(1, Palo.Basto);
        var sieteEspada = new Carta(7, Palo.Espada);
        var sieteOro = new Carta(7, Palo.Oro);
        var tres = new Carta(3, Palo.Copa);
        var dos = new Carta(2, Palo.Copa);
        var anchoFalso = new Carta(1, Palo.Copa);
        var doce = new Carta(12, Palo.Copa);
        var once = new Carta(11, Palo.Copa);
        var diez = new Carta(10, Palo.Copa);
        var sieteBasto = new Carta(7, Palo.Basto);
        var seisComun = new Carta(6, Palo.Basto);
        var cinco = new Carta(5, Palo.Copa);
        var cuatro = new Carta(4, Palo.Copa);

        Assert.True(gestor.ValorTruco(anchoEspada) > gestor.ValorTruco(anchoBasto));
        Assert.True(gestor.ValorTruco(anchoBasto) > gestor.ValorTruco(sieteEspada));
        Assert.True(gestor.ValorTruco(sieteEspada) > gestor.ValorTruco(sieteOro));
        Assert.True(gestor.ValorTruco(sieteOro) > gestor.ValorTruco(tres));
        Assert.True(gestor.ValorTruco(tres) > gestor.ValorTruco(dos));
        Assert.True(gestor.ValorTruco(dos) > gestor.ValorTruco(anchoFalso));
        Assert.True(gestor.ValorTruco(anchoFalso) > gestor.ValorTruco(doce));
        Assert.True(gestor.ValorTruco(doce) > gestor.ValorTruco(once));
        Assert.True(gestor.ValorTruco(once) > gestor.ValorTruco(diez));
        Assert.True(gestor.ValorTruco(diez) > gestor.ValorTruco(sieteBasto));
        Assert.True(gestor.ValorTruco(sieteBasto) > gestor.ValorTruco(seisComun));
        Assert.True(gestor.ValorTruco(seisComun) > gestor.ValorTruco(cinco));
        Assert.True(gestor.ValorTruco(cinco) > gestor.ValorTruco(cuatro));
    }

    [Fact]
    public void ValorTruco_EmpatesClasicos()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        Assert.Equal(gestor.ValorTruco(new Carta(1, Palo.Copa)), gestor.ValorTruco(new Carta(1, Palo.Oro)));
        Assert.Equal(gestor.ValorTruco(new Carta(7, Palo.Copa)), gestor.ValorTruco(new Carta(7, Palo.Basto)));
    }

    [Fact]
    public void ValorTruco_DocePromovido_OcupaElRangoDeLaPiezaQueReemplaza()
    {
        var gestor = new GestorDeJerarquia(new Carta(4, Palo.Oro)); // pieza Cuatro

        var doceDeOro = new Carta(12, Palo.Oro);
        var dosDeOro = new Carta(2, Palo.Oro);   // pieza Dos, sigue existiendo normal
        var cincoDeOro = new Carta(5, Palo.Oro); // pieza Cinco, sigue existiendo normal

        Assert.True(gestor.ValorTruco(dosDeOro) > gestor.ValorTruco(doceDeOro));
        Assert.True(gestor.ValorTruco(doceDeOro) > gestor.ValorTruco(cincoDeOro));
    }

    [Fact]
    public void Comparar_DevuelvePositivoNegativoOCero()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        var fuerte = new Carta(1, Palo.Espada);
        var debil = new Carta(4, Palo.Copa);
        var empateA = new Carta(1, Palo.Copa);
        var empateB = new Carta(1, Palo.Oro);

        Assert.True(gestor.Comparar(fuerte, debil) > 0);
        Assert.True(gestor.Comparar(debil, fuerte) < 0);
        Assert.Equal(0, gestor.Comparar(empateA, empateB));
    }
}
