using TrucoUruguayo.Bot.Modelo;
using Xunit;

namespace TrucoUruguayo.Bot.Tests.Modelo;

public class NivelCalculadoraTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 100)]
    [InlineData(3, 300)]
    [InlineData(4, 600)]
    public void XpParaAlcanzarNivel_DevuelveElUmbralAcumulado(int nivel, int esperado)
    {
        Assert.Equal(esperado, NivelCalculadora.XpParaAlcanzarNivel(nivel));
    }
}
