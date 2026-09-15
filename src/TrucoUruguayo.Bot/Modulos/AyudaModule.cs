using Discord;
using Discord.Interactions;

namespace TrucoUruguayo.Bot.Modulos;

public class AyudaModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ayuda", "Muestra las reglas y el valor de las cartas")]
    public async Task AyudaAsync()
    {
        var componentes = new ComponentBuilder()
            .WithButton("📖 Reglas Básicas", "ayuda_reglas", ButtonStyle.Primary)
            .WithButton("🃏 Valor de las Cartas", "ayuda_cartas", ButtonStyle.Secondary)
            .Build();

        await RespondAsync(
            "📚 Elegí qué querés consultar:",
            components: componentes,
            ephemeral: true);
    }

    [ComponentInteraction("ayuda_reglas")]
    public async Task MostrarReglas()
    {
        var embed = new EmbedBuilder()
            .WithTitle("📖 Reglas del Truco Uruguayo")
            .AddField("Objetivo", "Llegar a los puntos acordados ganando manos y sumando con el Envido.")
            .AddField("La Muestra", "Al inicio se da vuelta una carta. El palo de esta carta define las 'Piezas', que son las cartas más fuertes del juego.")
            .AddField("Envido", "Se canta en la primera mano. Suma el valor de dos cartas del mismo palo + 20. Si tenés una Pieza, suma su valor especial + la carta más alta.")
            .AddField("Truco", "El desafío por los puntos de la mano. Se puede escalar a Retruco y Vale 4.")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    [ComponentInteraction("ayuda_cartas")]
    public async Task MostrarCartas()
    {
        var embed = new EmbedBuilder()
            .WithTitle("🃏 Jerarquía y Tantos (de mayor a menor)")
            .AddField("🏆 LAS PIEZAS (Palo de la Muestra)",
                "2 de la muestra (30 tantos)\n4 de la muestra (29 tantos)\n5 de la muestra (28 tantos)\n11 - Caballo (27 tantos)\n10 - Sota (27 tantos)")
            .AddField("⚔️ LAS MATAS",
                "As de Espada (1 tanto)\nAs de Basto (1 tanto)\n7 de Espada (7 tantos)\n7 de Oro (7 tantos)")
            .AddField("🔽 CARTAS COMUNES (Valen su número para el envido)",
                "Todos los 3\nTodos los 2\nAses falsos (Copa/Oro)\n12 (Reyes) - 0 tantos\n11 y 10 falsos - 0 tantos\n7 falsos (Copa/Basto)\nLos 6, 5 y 4")
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

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
