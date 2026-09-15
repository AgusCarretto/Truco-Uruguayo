using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Modelo;

namespace TrucoUruguayo.Bot.Modulos;

public class PerfilModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UsuarioRepository _usuarioRepository;
    private readonly TiendaRepository _tiendaRepository;

    public PerfilModule(UsuarioRepository usuarioRepository, TiendaRepository tiendaRepository)
    {
        _usuarioRepository = usuarioRepository;
        _tiendaRepository = tiendaRepository;
    }

    [SlashCommand("perfil", "Mira tus estadisticas y monedas")]
    public async Task PerfilAsync([Summary("usuario", "De quien ver el perfil")] IUser? usuario = null)
    {
        var objetivo = usuario ?? Context.User;
        var usuarioDb = await _usuarioRepository.ObtenerUsuarioAsync(objetivo.Id);

        if (usuarioDb is null)
        {
            await RespondAsync($"❓ {objetivo.Mention} todavía no jugó nunca con el bot.", ephemeral: true);
            return;
        }

        var inventario = await _tiendaRepository.ObtenerInventarioAsync(objetivo.Id);
        var equipados = inventario.Where(item => item.Equipado).ToList();

        var xpNivelActual = usuarioDb.Xp - NivelCalculadora.XpParaAlcanzarNivel(usuarioDb.Nivel);
        var xpNecesaria = usuarioDb.Nivel * 100;

        var embed = new EmbedBuilder()
            .WithTitle($"👤 {usuarioDb.Nombre}")
            .AddField("🪙 Monedas", usuarioDb.Monedas, true)
            .AddField("✅ Victorias", usuarioDb.Victorias, true)
            .AddField("❌ Derrotas", usuarioDb.Derrotas, true)
            .AddField("🎮 Nivel y Experiencia", $"**Nivel {usuarioDb.Nivel}**\n{GenerarBarraExp(xpNivelActual, xpNecesaria)}")
            .WithColor(Color.Gold);

        if (equipados.Count > 0)
        {
            embed.AddField("🏆 Equipamiento Activo", string.Join('\n', equipados.Select(item => item.Nombre)));
        }

        await RespondAsync(embed: embed.Build());
    }

    private string GenerarBarraExp(int expActual, int expNecesaria, int longitudBarra = 10)
    {
        double porcentaje = Math.Clamp((double)expActual / expNecesaria, 0, 1);
        int bloquesLlenos = (int)Math.Round(porcentaje * longitudBarra);
        int bloquesVacios = longitudBarra - bloquesLlenos;
        return $"[{new string('█', bloquesLlenos)}{new string('░', bloquesVacios)}] {expActual}/{expNecesaria} XP";
    }
}
