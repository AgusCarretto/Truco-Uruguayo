using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;

namespace TrucoUruguayo.Bot.Modulos;

public class EconomiaModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UsuarioRepository _usuarioRepository;

    public EconomiaModule(UsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    [SlashCommand("diaria", "Reclama tus 500 monedas de regalo cada 24 horas")]
    public async Task DiariaAsync()
    {
        var (exito, tiempoRestante) = await _usuarioRepository.ReclamarDiariaAsync(Context.User.Id);

        if (exito)
        {
            await RespondAsync("🎁 ¡Reclamaste tus 🪙 500 monedas diarias! Volvé mañana.");
        }
        else
        {
            await RespondAsync(
                $"⏳ Todavía no podés reclamar. Volvé en {tiempoRestante!.Value.Hours} horas y {tiempoRestante.Value.Minutes} minutos.",
                ephemeral: true);
        }
    }
}
