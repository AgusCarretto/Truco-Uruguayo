using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Servicios;
using TrucoUruguayo.Core.Juego;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Bot.Modulos;

public class TrucoModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly GestorPartidas _gestorPartidas;
    private readonly UsuarioRepository _usuarioRepository;
    private readonly GeneradorImagenes _generadorImagenes;

    public TrucoModule(GestorPartidas gestorPartidas, UsuarioRepository usuarioRepository, GeneradorImagenes generadorImagenes)
    {
        _gestorPartidas = gestorPartidas;
        _usuarioRepository = usuarioRepository;
        _generadorImagenes = generadorImagenes;
    }

    [SlashCommand("truco", "Desafia a otro jugador a una partida de Truco")]
    public async Task TrucoAsync(
        [Summary("usuario", "A quien desafias")] IUser usuario,
        [Summary("apuesta", "Cuantas monedas se apuestan")] int apuesta)
    {
        var retadorId = Context.User.Id;
        var retadoId = usuario.Id;

        if (usuario.IsBot)
        {
            await RespondAsync("🤖 No podés desafiar a un bot.", ephemeral: true);
            return;
        }

        if (retadoId == retadorId)
        {
            await RespondAsync("🙃 No podés desafiarte a vos mismo.", ephemeral: true);
            return;
        }

        if (apuesta <= 0)
        {
            await RespondAsync("🪙 La apuesta tiene que ser mayor a 0.", ephemeral: true);
            return;
        }

        if (_gestorPartidas.ObtenerPartidaPorUsuario(retadorId) is not null)
        {
            await RespondAsync("⚠️ Ya estás jugando una partida.", ephemeral: true);
            return;
        }

        if (_gestorPartidas.ObtenerPartidaPorUsuario(retadoId) is not null)
        {
            await RespondAsync($"⚠️ {usuario.Mention} ya está jugando otra partida.", ephemeral: true);
            return;
        }

        var usuarioRetador = (await _usuarioRepository.ObtenerUsuarioAsync(retadorId))!;
        var usuarioRetado = await _usuarioRepository.ObtenerUsuarioAsync(retadoId);

        if (usuarioRetado is null)
        {
            await RespondAsync($"❓ {usuario.Mention} todavía no jugó nunca con el bot.", ephemeral: true);
            return;
        }

        if (usuarioRetador.Monedas < apuesta)
        {
            await RespondAsync("💸 No tenés suficientes monedas para esa apuesta.", ephemeral: true);
            return;
        }

        if (usuarioRetado.Monedas < apuesta)
        {
            await RespondAsync($"💸 {usuario.Mention} no tiene suficientes monedas para esa apuesta.", ephemeral: true);
            return;
        }

        var componentes = new ComponentBuilder()
            .WithButton("✅ Aceptar", $"reto_aceptar_{retadorId}_{retadoId}_{apuesta}", ButtonStyle.Success)
            .Build();

        await RespondAsync(
            $"⚔️ {usuario.Mention}, {Context.User.Mention} te desafía a una partida de Truco por 🪙 {apuesta} monedas!",
            components: componentes);
    }

    [ComponentInteraction("reto_aceptar_*_*_*")]
    public async Task AceptarReto(ulong retadorId, ulong retadoId, int apuesta)
    {
        if (Context.User.Id != retadoId)
        {
            await RespondAsync("🚫 Solo el retado puede aceptar.", ephemeral: true);
            return;
        }

        if (_gestorPartidas.ObtenerPartidaPorUsuario(retadorId) is not null
            || _gestorPartidas.ObtenerPartidaPorUsuario(retadoId) is not null)
        {
            await RespondAsync("⚠️ Ya no se puede aceptar este reto.", ephemeral: true);
            return;
        }

        await _usuarioRepository.ActualizarMonedasAsync(retadorId, -apuesta);
        await _usuarioRepository.ActualizarMonedasAsync(retadoId, -apuesta);

        _gestorPartidas.IniciarPartida(Context.Channel.Id, retadorId, retadoId);
        _gestorPartidas.ApuestasActivas[Context.Channel.Id] = apuesta;
        var ronda = _gestorPartidas.ObtenerPartidaPorUsuario(retadoId)!;

        await ((SocketMessageComponent)Context.Interaction).UpdateAsync(mensaje => mensaje.Components = new ComponentBuilder().Build());

        await using var streamMesa = await _generadorImagenes.GenerarMesaAsync(ronda.Muestra);
        await Context.Channel.SendFileAsync(
            streamMesa,
            "mesa.png",
            text: $"🎉 ¡Partida iniciada por 🪙 {apuesta} monedas! 🃏 La muestra es **{ronda.Muestra}**. 👉 Turno de <@{retadorId}>.",
            components: ConstruirBotonesDeAccion(ronda));
    }

    [ComponentInteraction("ver_mano")]
    public async Task VerMano()
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        var userId = Context.User.Id;
        IReadOnlyList<Carta> mano;

        if (userId == ronda.Jugador1Id)
        {
            mano = ronda.ManoJugador1;
        }
        else if (userId == ronda.Jugador2Id)
        {
            mano = ronda.ManoJugador2;
        }
        else
        {
            await RespondAsync("🚫 No sos parte de esta partida.", ephemeral: true);
            return;
        }

        var componentes = new ComponentBuilder();
        for (var i = 0; i < mano.Count; i++)
        {
            componentes.WithButton(mano[i].ToString(), $"jugar_carta_{i}", ButtonStyle.Primary);
        }

        await using var streamMano = await _generadorImagenes.GenerarManoAsync(mano);
        await RespondWithFileAsync(streamMano, "mano.png", components: componentes.Build(), ephemeral: true);
    }

    [ComponentInteraction("jugar_carta_*")]
    public async Task JugarCarta(int index)
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        if (Context.User.Id != ronda.TurnoActual)
        {
            await RespondAsync("⏳ No es tu turno.", ephemeral: true);
            return;
        }

        var mano = Context.User.Id == ronda.Jugador1Id ? ronda.ManoJugador1 : ronda.ManoJugador2;
        var carta = mano[index];
        var faseAntes = ronda.Fase;

        ronda.JugarCarta(Context.User.Id, carta);

        if (ronda.Fase == FaseRonda.Finalizada)
        {
            await FinalizarPartidaYPagarAsync(ronda);
        }
        else if (ronda.Fase != faseAntes)
        {
            await Context.Channel.SendMessageAsync(
                $"🏁 Mano terminada. 🏆 Ganador de la mano: <@{ronda.TurnoActual}>. 👉 Turno de <@{ronda.TurnoActual}>.",
                components: ConstruirBotonesDeAccion(ronda));
        }
        else
        {
            await Context.Channel.SendMessageAsync(
                $"🎴 <@{Context.User.Id}> jugó **{carta}**. 👉 Turno de <@{ronda.TurnoActual}>.",
                components: ConstruirBotonesDeAccion(ronda));
        }

        await DeferAsync();
    }

    [ComponentInteraction("cantar_envido")]
    public async Task CantarEnvidoAsync()
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        try
        {
            ronda.CantarEnvido(Context.User.Id, Canto.Envido);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        var botones = new ComponentBuilder()
            .WithButton("✅ Quiero", "resp_envido_quiero", ButtonStyle.Success)
            .WithButton("❌ No Quiero", "resp_envido_noquiero", ButtonStyle.Danger)
            .Build();

        await Context.Channel.SendMessageAsync($"🎲 <@{Context.User.Id}> tocó Envido!", components: botones);
        await DeferAsync();
    }

    [ComponentInteraction("gritar_truco")]
    public async Task GritarTrucoAsync()
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        if (Context.User.Id != ronda.Jugador1Id && Context.User.Id != ronda.Jugador2Id)
        {
            await RespondAsync("🚫 No sos parte de esta partida.", ephemeral: true);
            return;
        }

        var canto = SiguienteCantoTruco(ronda.ValorTrucoActual);

        try
        {
            ronda.GritarTruco(Context.User.Id, canto);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        var botones = new ComponentBuilder()
            .WithButton("✅ Quiero", "resp_truco_quiero", ButtonStyle.Success)
            .WithButton("❌ No Quiero", "resp_truco_noquiero", ButtonStyle.Danger);

        var siguiente = SiguienteEscalada(canto);
        if (siguiente is not null)
        {
            botones.WithButton($"🔥 {NombreCantoTruco(siguiente.Value)}", "gritar_truco", ButtonStyle.Primary);
        }

        await Context.Channel.SendMessageAsync(
            $"🔥 <@{Context.User.Id}> gritó **{NombreCantoTruco(canto)}**!",
            components: botones.Build());
        await DeferAsync();
    }

    [ComponentInteraction("irse_mazo")]
    public async Task IrseAlMazoAsync()
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        if (Context.User.Id != ronda.Jugador1Id && Context.User.Id != ronda.Jugador2Id)
        {
            await RespondAsync("🚫 No sos parte de esta partida.", ephemeral: true);
            return;
        }

        try
        {
            ronda.IrseAlMazo(Context.User.Id);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        await FinalizarPartidaYPagarAsync(ronda);
        await DeferAsync();
    }

    [ComponentInteraction("resp_envido_*")]
    public async Task ResponderEnvidoAsync(string accion)
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        var quienResponde = ronda.JugadorQueCanto == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;

        if (Context.User.Id != quienResponde)
        {
            await RespondAsync("🚫 No sos quien tiene que responder este canto.", ephemeral: true);
            return;
        }

        var respuesta = accion == "quiero" ? RespuestaCanto.Quiero : RespuestaCanto.NoQuiero;
        var puntosJugador1Antes = ronda.PuntosJugador1;

        try
        {
            ronda.ResponderEnvido(Context.User.Id, respuesta);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        var puntosGanadosJugador1 = ronda.PuntosJugador1 - puntosJugador1Antes;
        var mensajePuntos = puntosGanadosJugador1 > 0
            ? $"🎲 <@{ronda.Jugador1Id}> se lleva {puntosGanadosJugador1} puntos de envido."
            : $"🎲 <@{ronda.Jugador2Id}> se lleva {ronda.PuntosJugador2} puntos de envido.";

        await Context.Channel.SendMessageAsync(
            $"{mensajePuntos} 👉 Turno de <@{ronda.TurnoActual}>.",
            components: ConstruirBotonesDeAccion(ronda));
        await DeferAsync();
    }

    [ComponentInteraction("resp_truco_*")]
    public async Task ResponderTrucoAsync(string accion)
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        var quienResponde = ronda.JugadorQueGritoTruco == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;

        if (Context.User.Id != quienResponde)
        {
            await RespondAsync("🚫 No sos quien tiene que responder este canto.", ephemeral: true);
            return;
        }

        var respuesta = accion == "quiero" ? RespuestaCanto.Quiero : RespuestaCanto.NoQuiero;

        try
        {
            ronda.ResponderTruco(Context.User.Id, respuesta);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        if (ronda.Fase == FaseRonda.Finalizada)
        {
            await FinalizarPartidaYPagarAsync(ronda);
        }
        else
        {
            await Context.Channel.SendMessageAsync(
                $"🔥 ¡Truco por {ronda.ValorTrucoActual}! 👉 Turno de <@{ronda.TurnoActual}>.",
                components: ConstruirBotonesDeAccion(ronda));
        }

        await DeferAsync();
    }

    private async Task FinalizarPartidaYPagarAsync(Ronda ronda)
    {
        var ganadorId = ronda.GanadorRonda!.Value;
        var perdedorId = ganadorId == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;
        var apuesta = _gestorPartidas.ApuestasActivas.GetValueOrDefault(Context.Channel.Id);
        var pozo = apuesta * 2;

        await _usuarioRepository.ActualizarMonedasAsync(ganadorId, pozo);
        await _usuarioRepository.RegistrarPartidaAsync(ganadorId, perdedorId, apuesta);
        await _usuarioRepository.SumarVictoriaAsync(ganadorId);
        await _usuarioRepository.SumarDerrotaAsync(perdedorId);

        _gestorPartidas.FinalizarPartida(Context.Channel.Id);

        await Context.Channel.SendMessageAsync(
            $"🏆 ¡Ronda finalizada! <@{ganadorId}> gana la partida y se lleva 🪙 {pozo} monedas!");
    }

    private static MessageComponent ConstruirBotonesDeAccion(Ronda ronda)
    {
        var botones = new ComponentBuilder()
            .WithButton("🃏 Ver mis cartas", "ver_mano", ButtonStyle.Primary);

        if (ronda.Estado == EstadoRonda.EsperandoEnvido || ronda.Estado == EstadoRonda.JugandoCartas)
        {
            if (ronda.Estado == EstadoRonda.EsperandoEnvido)
            {
                botones.WithButton("🎲 Tocar Envido", "cantar_envido", ButtonStyle.Secondary);
            }

            if (ronda.ValorTrucoActual < 4)
            {
                botones.WithButton(
                    $"🔥 {NombreCantoTruco(SiguienteCantoTruco(ronda.ValorTrucoActual))}",
                    "gritar_truco",
                    ButtonStyle.Secondary);
            }

            botones.WithButton("🏳️ Irse al Mazo", "irse_mazo", ButtonStyle.Danger);
        }

        return botones.Build();
    }

    private static CantoTruco SiguienteCantoTruco(int valorTrucoActual) => valorTrucoActual switch
    {
        1 => CantoTruco.Truco,
        2 => CantoTruco.Retruco,
        _ => CantoTruco.ValeCuatro,
    };

    private static CantoTruco? SiguienteEscalada(CantoTruco actual) => actual switch
    {
        CantoTruco.Truco => CantoTruco.Retruco,
        CantoTruco.Retruco => CantoTruco.ValeCuatro,
        _ => null,
    };

    private static string NombreCantoTruco(CantoTruco canto) => canto switch
    {
        CantoTruco.Truco => "Truco",
        CantoTruco.Retruco => "Retruco",
        _ => "Vale 4",
    };
}
