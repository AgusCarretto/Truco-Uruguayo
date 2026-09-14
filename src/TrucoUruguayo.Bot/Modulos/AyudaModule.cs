using Discord;
using Discord.Interactions;

namespace TrucoUruguayo.Bot.Modulos;

public class AyudaModule : InteractionModuleBase<SocketInteractionContext>
{
    [ComponentInteraction("como_jugar")]
    public async Task ComoJugarAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("❓ ¿Cómo se juega?")
            .WithDescription(
                "Podés jugar de varias formas:\n\n" +
                "⚔️ Desafiando directo a alguien con `/truco @persona` para arrancar una partida 1 contra 1.\n" +
                "👤 `/perfil` para ver tus monedas, victorias y derrotas.\n" +
                "🛒 `/tienda`, `/comprar` y `/inventario` para gastar tus monedas en items.\n" +
                "🎁 `/diaria` para reclamar 500 monedas gratis cada 24 horas.\n" +
                "🏅 `/ranking` para ver el top global, y `/historial` para tus últimas partidas.")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}
