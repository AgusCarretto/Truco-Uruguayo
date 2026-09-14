using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;

namespace TrucoUruguayo.Bot.Modulos;

public class RankingModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UsuarioRepository _usuarioRepository;

    public RankingModule(UsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    [SlashCommand("ranking", "Mira el top 10 global")]
    public async Task RankingAsync(
        [Summary("categoria", "Por que ordenar el ranking")]
        [Choice("Monedas", "monedas")]
        [Choice("Experiencia", "xp")]
        string categoria)
    {
        var usuarios = await _usuarioRepository.ObtenerTopUsuariosAsync(categoria);

        var categoriaLabel = categoria == "xp" ? "Experiencia" : "Monedas";
        var lineas = usuarios.Select((usuario, indice) =>
        {
            var puesto = indice switch
            {
                0 => "🥇",
                1 => "🥈",
                2 => "🥉",
                _ => $"#{indice + 1}",
            };

            return $"{puesto} {usuario.Nombre} | 🪙 {usuario.Monedas} | ✨ {usuario.Xp} XP";
        });

        var embed = new EmbedBuilder()
            .WithTitle($"🏅 Top 10 Global - {categoriaLabel}")
            .WithDescription(string.Join('\n', lineas))
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed);
    }
}
