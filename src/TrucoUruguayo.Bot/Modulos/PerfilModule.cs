using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Modelo;
using TrucoUruguayo.Bot.Servicios;

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

        var tituloEmbed = usuarioDb.TituloEquipado is not null
            ? $"👤 {usuarioDb.Nombre} | 🏆 {usuarioDb.TituloEquipado}"
            : $"👤 {usuarioDb.Nombre}";

        var embed = new EmbedBuilder()
            .WithTitle(tituloEmbed)
            .AddField("🪙 Monedas", usuarioDb.Monedas, true)
            .AddField("✅ Victorias", usuarioDb.Victorias, true)
            .AddField("❌ Derrotas", usuarioDb.Derrotas, true)
            .AddField("🎮 Nivel y Experiencia", $"**Nivel {usuarioDb.Nivel}**\n{GenerarBarraExp(xpNivelActual, xpNecesaria)}")
            .AddField("🃏 Mazo equipado", NombreLegibleMazo(usuarioDb.MazoEquipado), true)
            .WithColor(Color.Gold);

        if (equipados.Count > 0)
        {
            embed.AddField("🏆 Equipamiento Activo", string.Join('\n', equipados.Select(item => item.Nombre)));
        }

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("titulos", "Mira que titulos tenes desbloqueados")]
    public async Task TitulosAsync()
    {
        var usuarioDb = await _usuarioRepository.ObtenerUsuarioAsync(Context.User.Id);

        if (usuarioDb is null)
        {
            await RespondAsync("❓ Todavía no jugaste nunca con el bot.", ephemeral: true);
            return;
        }

        var lineas = ConstantesTitulos.TitulosPorNivel
            .OrderBy(kv => kv.Key)
            .Select(kv => usuarioDb.Nivel >= kv.Key
                ? $"✅ **{kv.Value}** (Nivel {kv.Key})"
                : $"🔒 {kv.Value} — se desbloquea en Nivel {kv.Key}");

        var embed = new EmbedBuilder()
            .WithTitle("🏆 Títulos")
            .WithDescription(string.Join('\n', lineas))
            .WithColor(Color.Gold);

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("titulo_equipar", "Equipa un titulo que hayas desbloqueado")]
    public async Task TituloEquiparAsync(
        [Summary("titulo", "Que titulo equipar")]
        [Choice("Pichón", "Pichón")]
        [Choice("Orejeador", "Orejeador")]
        [Choice("Bocón", "Bocón")]
        [Choice("Rey del Envido", "Rey del Envido")]
        [Choice("Cebador de Canarias Suave", "Cebador de Canarias Suave")]
        [Choice("Asador Oficial", "Asador Oficial")]
        [Choice("Maestro del Retruco", "Maestro del Retruco")]
        [Choice("Dueño de la Muestra", "Dueño de la Muestra")]
        [Choice("Leyenda del Truco", "Leyenda del Truco")]
        string titulo)
    {
        var exito = await _usuarioRepository.EquiparTituloAsync(Context.User.Id, titulo);

        if (!exito)
        {
            await RespondAsync("🔒 Todavía no tenés el nivel para ese título.", ephemeral: true);
            return;
        }

        await RespondAsync($"✅ Ahora tenés equipado: **{titulo}**");
    }

    [SlashCommand("mazo_equipar", "Elegi con que mazo se ven tus cartas")]
    public async Task MazoEquiparAsync(
        [Summary("mazo", "Que mazo equipar")]
        [Choice("Básico", GeneradorImagenes.MazoBasico)]
        [Choice("Clásico", GeneradorImagenes.MazoClasico)]
        string mazo)
    {
        var exito = await _usuarioRepository.EquiparMazoAsync(Context.User.Id, mazo);

        if (!exito)
        {
            await RespondAsync("🔒 Todavía no compraste ese mazo en la tienda.", ephemeral: true);
            return;
        }

        await RespondAsync($"✅ Ahora tenés equipado el mazo: **{NombreLegibleMazo(mazo)}**");
    }

    private static string NombreLegibleMazo(string mazo) =>
        mazo == GeneradorImagenes.MazoClasico ? "Clásico" : "Básico";

    private string GenerarBarraExp(int expActual, int expNecesaria, int longitudBarra = 10)
    {
        double porcentaje = Math.Clamp((double)expActual / expNecesaria, 0, 1);
        int bloquesLlenos = (int)Math.Round(porcentaje * longitudBarra);
        int bloquesVacios = longitudBarra - bloquesLlenos;
        return $"[{new string('█', bloquesLlenos)}{new string('░', bloquesVacios)}] {expActual}/{expNecesaria} XP";
    }
}
