using Discord;
using Discord.Interactions;

namespace TrucoUruguayo.Bot.Modulos;

public class AyudaModule : InteractionModuleBase<SocketInteractionContext>
{
    [ComponentInteraction("como_jugar")]
    public async Task ComoJugarAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("¿Cómo se juega?")
            .WithDescription(
                "Podés jugar de dos formas:\n\n" +
                "• Con comandos como `/perfil` para ver tus monedas, victorias y derrotas.\n" +
                "• Desafiando directo a alguien con `/truco @persona` para arrancar una partida 1 contra 1.\n\n" +
                "*(`/truco` todavía lo estamos armando — por ahora `/perfil` ya anda)*")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}
