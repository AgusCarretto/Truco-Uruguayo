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
        var usuario = await _usuarioRepository.ObtenerUsuarioAsync(Context.User.Id)
            ?? await _usuarioRepository.RegistrarUsuarioAsync(Context.User.Id, Context.User.Username);

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
