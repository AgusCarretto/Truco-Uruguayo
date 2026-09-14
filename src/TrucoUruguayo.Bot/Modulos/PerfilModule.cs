using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;

namespace TrucoUruguayo.Bot.Modulos;

public class PerfilModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UsuarioRepository _usuarioRepository;

    public PerfilModule(UsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    [SlashCommand("perfil", "Mira tus estadisticas y monedas")]
    public async Task PerfilAsync()
    {
        // El interceptor global en Program.cs garantiza que el usuario ya existe antes de llegar aca.
        var usuario = (await _usuarioRepository.ObtenerUsuarioAsync(Context.User.Id))!;

        var embed = new EmbedBuilder()
            .WithTitle(usuario.Nombre)
            .AddField("Monedas", usuario.Monedas, true)
            .AddField("Victorias", usuario.Victorias, true)
            .AddField("Derrotas", usuario.Derrotas, true)
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed);
    }
}
