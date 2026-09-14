using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;

namespace TrucoUruguayo.Bot.Modulos;

public class HistorialModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UsuarioRepository _usuarioRepository;

    public HistorialModule(UsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    [SlashCommand("historial", "Mira tus ultimas partidas")]
    public async Task HistorialAsync(
        [Summary("usuario", "De quien ver el historial")] IUser? usuario = null)
    {
        var objetivo = usuario ?? Context.User;
        var partidas = await _usuarioRepository.ObtenerHistorialAsync(objetivo.Id);

        var lineas = partidas.Select(partida =>
        {
            var esGanador = (ulong)partida.GanadorId == objetivo.Id;
            var rivalId = esGanador ? (ulong)partida.PerdedorId : (ulong)partida.GanadorId;
            var resultado = esGanador ? "✅ Victoria" : "❌ Derrota";

            return $"🗓️ [{partida.Fecha:dd/MM/yyyy HH:mm}] vs <@{rivalId}> | {resultado} | 🪙 {partida.Apuesta}";
        });

        var embed = new EmbedBuilder()
            .WithTitle($"📜 Últimas partidas de {objetivo.Username}")
            .WithDescription(string.Join('\n', lineas))
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed);
    }
}
