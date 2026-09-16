using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Bot.Servicios;

public class GeneradorImagenes
{
    private const int AnchoCarta = 100;
    private const int AnchoMuestraEnMesa = 75;
    private const int AnchoJugadaEnMesa = 110;
    private const int MargenEntreCartas = 10;
    private const int MargenEntreGrupos = 30;
    private const int MargenPilaDorso = 6;
    private const int CantidadDorsosEnPila = 2;

    private static readonly string CarpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas", "mazo_basico");
    private static readonly string RutaDorso = Path.Combine(AppContext.BaseDirectory, "assets", "cartas", "dorso.png");
    private static readonly string RutaFondoMadera = Path.Combine(AppContext.BaseDirectory, "assets", "fondos", "fondo_madera.jpg");

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

    public async Task<MemoryStream> GenerarMesaActualAsync(Carta muestra, Carta? jugada1, Carta? jugada2)
    {
        using var imagenMuestra = await CargarCartaAsync(muestra, AnchoMuestraEnMesa);
        using var imagenDorso = await CargarDorsoAsync(AnchoMuestraEnMesa);
        using var imagenJugada1 = jugada1 is not null ? await CargarCartaAsync(jugada1, AnchoJugadaEnMesa) : null;
        using var imagenJugada2 = jugada2 is not null ? await CargarCartaAsync(jugada2, AnchoJugadaEnMesa) : null;

        var jugadas = new List<Image<Rgba32>>();
        if (imagenJugada1 is not null)
        {
            jugadas.Add(imagenJugada1);
        }

        if (imagenJugada2 is not null)
        {
            jugadas.Add(imagenJugada2);
        }

        // La pila de dorso asoma detras de la muestra (como el mazo boca abajo), agregando
        // el desplazamiento maximo de sus copias al ancho y alto del grupo de la muestra.
        var extraPila = MargenPilaDorso * CantidadDorsosEnPila;
        var anchoMuestraConPila = imagenMuestra.Width + extraPila;
        var altoMuestraConPila = imagenMuestra.Height + extraPila;

        var anchoJugadas = jugadas.Sum(imagen => imagen.Width) + MargenEntreCartas * Math.Max(0, jugadas.Count - 1);
        var espacioAntesDeJugadas = jugadas.Count > 0 ? MargenEntreGrupos : 0;
        var anchoTotal = anchoMuestraConPila + espacioAntesDeJugadas + anchoJugadas;
        var altoMaximo = Math.Max(altoMuestraConPila, jugadas.Count > 0 ? jugadas.Max(imagen => imagen.Height) : 0);

        using var imagenFondo = await CargarFondoMaderaAsync(anchoTotal, altoMaximo);
        using var lienzo = new Image<Rgba32>(anchoTotal, altoMaximo);

        lienzo.Mutate(contexto =>
        {
            contexto.DrawImage(imagenFondo, new Point(0, 0), 1f);

            // El grupo muestra+pila es mas chico que las jugadas: se centra verticalmente, lo
            // que lo deja un poco mas abajo que el borde superior donde arrancan las jugadas.
            var offsetYGrupoMuestra = (altoMaximo - altoMuestraConPila) / 2;

            for (var i = 1; i <= CantidadDorsosEnPila; i++)
            {
                var desplazamiento = MargenPilaDorso * i;
                contexto.DrawImage(imagenDorso, new Point(desplazamiento, offsetYGrupoMuestra + desplazamiento), 1f);
            }

            contexto.DrawImage(imagenMuestra, new Point(0, offsetYGrupoMuestra), 1f);

            var posicionX = anchoMuestraConPila + espacioAntesDeJugadas;
            foreach (var imagenJugada in jugadas)
            {
                contexto.DrawImage(imagenJugada, new Point(posicionX, (altoMaximo - imagenJugada.Height) / 2), 1f);
                posicionX += imagenJugada.Width + MargenEntreCartas;
            }
        });

        var streamResultado = new MemoryStream();
        await lienzo.SaveAsPngAsync(streamResultado);
        streamResultado.Position = 0;
        return streamResultado;
    }

    private static async Task<Image<Rgba32>> CargarCartaAsync(Carta carta, int ancho = AnchoCarta)
    {
        var ruta = Path.Combine(CarpetaCartas, $"{carta.Numero}_{carta.Palo.ToString().ToLowerInvariant()}.png");
        var imagen = await Image.LoadAsync<Rgba32>(ruta);
        imagen.Mutate(x => x.Resize(ancho, 0));
        return imagen;
    }

    private static async Task<Image<Rgba32>> CargarDorsoAsync(int ancho)
    {
        var imagen = await Image.LoadAsync<Rgba32>(RutaDorso);
        imagen.Mutate(x => x.Resize(ancho, 0));
        return imagen;
    }

    private static async Task<Image<Rgba32>> CargarFondoMaderaAsync(int ancho, int alto)
    {
        var imagen = await Image.LoadAsync<Rgba32>(RutaFondoMadera);
        imagen.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(ancho, alto),
            Mode = ResizeMode.Crop,
        }));
        return imagen;
    }
}
