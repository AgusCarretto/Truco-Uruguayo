using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using TrucoUruguayo.Bot.Servicios;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Bot.Tests.Servicios;

public class GeneradorImagenesTests
{
    private const int AnchoCartaEsperado = 100;

    [Fact]
    public async Task GenerarMesaActualAsync_SoloMuestra_DevuelveElLienzoDeTamanoFijo()
    {
        var generador = new GeneradorImagenes();

        await using var stream = await generador.GenerarMesaActualAsync(new Carta(1, Palo.Espada), null, null);

        Assert.Equal(0, stream.Position);
        using var imagen = await Image.LoadAsync(stream);
        Assert.Equal(GeneradorImagenes.AnchoLienzoMesa, imagen.Width);
        Assert.Equal(GeneradorImagenes.AltoLienzoMesa, imagen.Height);
    }

    [Fact]
    public async Task GenerarMesaActualAsync_ConLasDosJugadas_MantieneElMismoTamanoDeLienzo()
    {
        var generador = new GeneradorImagenes();

        await using var stream = await generador.GenerarMesaActualAsync(
            new Carta(3, Palo.Oro), new Carta(1, Palo.Espada), new Carta(7, Palo.Copa));

        using var imagen = await Image.LoadAsync(stream);

        // El lienzo es de tamano fijo: no crece aunque haya mas cartas en juego.
        Assert.Equal(GeneradorImagenes.AnchoLienzoMesa, imagen.Width);
        Assert.Equal(GeneradorImagenes.AltoLienzoMesa, imagen.Height);
    }

    [Fact]
    public async Task GenerarManoAsync_TresCartas_ElAnchoEsLaSumaMasMargenes()
    {
        var generador = new GeneradorImagenes();
        var mano = new[]
        {
            new Carta(1, Palo.Espada),
            new Carta(7, Palo.Oro),
            new Carta(12, Palo.Copa),
        };

        await using var stream = await generador.GenerarManoAsync(mano);

        using var imagenCombinada = await Image.LoadAsync(stream);

        var carpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas", "mazo_basico");
        using var carta1 = await CargarYRedimensionarAsync(Path.Combine(carpetaCartas, "1_espada.png"));
        using var carta2 = await CargarYRedimensionarAsync(Path.Combine(carpetaCartas, "7_oro.png"));
        using var carta3 = await CargarYRedimensionarAsync(Path.Combine(carpetaCartas, "12_copa.png"));

        // Las cartas reales no miden todas exactamente lo mismo despues de redimensionar
        // (se recortaron de una lamina con proporciones levemente distintas), por eso el
        // ancho esperado se suma de cada archivo en vez de asumir un ancho uniforme.
        var anchoEsperado = carta1.Width + carta2.Width + carta3.Width + 10 * 2;
        var altoEsperado = Math.Max(carta1.Height, Math.Max(carta2.Height, carta3.Height));

        Assert.Equal(anchoEsperado, imagenCombinada.Width);
        Assert.Equal(altoEsperado, imagenCombinada.Height);
    }

    [Fact]
    public async Task GenerarManoAsync_ConMazoClasico_CargaLosJpgYNoRompe()
    {
        var generador = new GeneradorImagenes();
        var mano = new[] { new Carta(1, Palo.Espada), new Carta(7, Palo.Oro) };

        await using var stream = await generador.GenerarManoAsync(mano, GeneradorImagenes.MazoClasico);

        using var imagen = await Image.LoadAsync(stream);
        Assert.True(imagen.Width > 0);
        Assert.True(imagen.Height > 0);
    }

    [Fact]
    public async Task GenerarMesaActualAsync_ConJugadasDeMazosDistintos_ComponeSinRomper()
    {
        var generador = new GeneradorImagenes();

        await using var stream = await generador.GenerarMesaActualAsync(
            new Carta(3, Palo.Oro),
            new Carta(1, Palo.Espada), new Carta(7, Palo.Copa),
            mazoJugada1: GeneradorImagenes.MazoClasico,
            mazoJugada2: GeneradorImagenes.MazoBasico);

        using var imagen = await Image.LoadAsync(stream);

        Assert.Equal(GeneradorImagenes.AnchoLienzoMesa, imagen.Width);
        Assert.Equal(GeneradorImagenes.AltoLienzoMesa, imagen.Height);
    }

    private static async Task<Image> CargarYRedimensionarAsync(string ruta)
    {
        var imagen = await Image.LoadAsync(ruta);
        imagen.Mutate(x => x.Resize(AnchoCartaEsperado, 0));
        return imagen;
    }
}
