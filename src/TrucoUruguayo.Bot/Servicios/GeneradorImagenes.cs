using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Bot.Servicios;

public class GeneradorImagenes
{
    private const int MargenEntreCartas = 10;

    private static readonly string CarpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas");

    public async Task<MemoryStream> GenerarManoAsync(IEnumerable<Carta> cartas)
    {
        var imagenesCartas = new List<Image<Rgba32>>();

        try
        {
            foreach (var carta in cartas)
            {
                imagenesCartas.Add(await CargarCartaAsync(carta));
            }

            var anchoTotal = imagenesCartas.Sum(imagen => imagen.Width) + MargenEntreCartas * Math.Max(0, imagenesCartas.Count - 1);
            var altoMaximo = imagenesCartas.Count > 0 ? imagenesCartas.Max(imagen => imagen.Height) : 0;

            using var lienzo = new Image<Rgba32>(anchoTotal, altoMaximo);

            lienzo.Mutate(contexto =>
            {
                var posicionX = 0;
                foreach (var imagenCarta in imagenesCartas)
                {
                    contexto.DrawImage(imagenCarta, new Point(posicionX, 0), 1f);
                    posicionX += imagenCarta.Width + MargenEntreCartas;
                }
            });

            var stream = new MemoryStream();
            await lienzo.SaveAsPngAsync(stream);
            stream.Position = 0;
            return stream;
        }
        finally
        {
            foreach (var imagenCarta in imagenesCartas)
            {
                imagenCarta.Dispose();
            }
        }
    }

    public async Task<MemoryStream> GenerarMesaAsync(Carta muestra)
    {
        using var imagenCarta = await CargarCartaAsync(muestra);

        var stream = new MemoryStream();
        await imagenCarta.SaveAsPngAsync(stream);
        stream.Position = 0;
        return stream;
    }

    private static async Task<Image<Rgba32>> CargarCartaAsync(Carta carta)
    {
        var ruta = Path.Combine(CarpetaCartas, $"{carta.Numero}_{carta.Palo.ToString().ToLowerInvariant()}.png");
        return await Image.LoadAsync<Rgba32>(ruta);
    }
}
