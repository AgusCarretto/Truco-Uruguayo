using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;

namespace TrucoUruguayo.Bot.Modulos;

public class TiendaModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TiendaRepository _tiendaRepository;

    public TiendaModule(TiendaRepository tiendaRepository)
    {
        _tiendaRepository = tiendaRepository;
    }

    [SlashCommand("tienda", "Mira los items disponibles para comprar")]
    public async Task TiendaAsync()
    {
        var items = await _tiendaRepository.ObtenerItemsTiendaAsync();

        var lineas = items.Select(item => $"🛍️ #{item.Id} - {item.Nombre} | 🪙 {item.Precio} monedas");

        var embed = new EmbedBuilder()
            .WithTitle("🛒 Tienda")
            .WithDescription(string.Join('\n', lineas))
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("comprar", "Compra un item de la tienda")]
    public async Task ComprarAsync([Summary("item_id", "El ID del item a comprar")] int itemId)
    {
        var resultado = await _tiendaRepository.ComprarItemAsync(Context.User.Id, itemId);

        var mensaje = resultado switch
        {
            ResultadoCompra.Exito => "✅ ¡Compra exitosa!",
            ResultadoCompra.SinFondos => "💸 No tenés suficientes monedas para ese item.",
            ResultadoCompra.YaComprado => "📦 Ya tenés ese item.",
            ResultadoCompra.ItemNoExiste => "❓ Ese item no existe.",
            _ => "⚠️ Ocurrió un error inesperado.",
        };

        await RespondAsync(mensaje, ephemeral: true);
    }

    [SlashCommand("inventario", "Mira tus items comprados")]
    public async Task InventarioAsync()
    {
        var items = await _tiendaRepository.ObtenerInventarioAsync(Context.User.Id);

        var lineas = items.Select(item =>
        {
            var indicador = item.Equipado ? " ⭐ [Equipado]" : "";
            return $"🎁 {item.Nombre} | 🗓️ Comprado: {item.FechaCompra:dd/MM/yyyy}{indicador}";
        });

        var embed = new EmbedBuilder()
            .WithTitle($"🎒 Inventario de {Context.User.Username}")
            .WithDescription(string.Join('\n', lineas))
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [SlashCommand("equipar", "Equipa o desequipa un item de tu inventario")]
    public async Task EquiparAsync([Summary("item_id", "El ID del item a equipar/desequipar")] int itemId)
    {
        var nuevoEstado = await _tiendaRepository.AlternarEquipamientoAsync(Context.User.Id, itemId);

        if (nuevoEstado is null)
        {
            await RespondAsync("❓ No tenés ese item en tu inventario.", ephemeral: true);
            return;
        }

        var mensaje = nuevoEstado.Value ? "⭐ Item equipado correctamente." : "📤 Item desequipado correctamente.";
        await RespondAsync(mensaje, ephemeral: true);
    }
}
