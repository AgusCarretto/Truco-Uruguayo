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
        [Summary("apuesta", "Cuantas monedas se apuestan")] int apuesta,
        [Summary("puntos", "A cuantos puntos se juega")]
        [Choice("10 Puntos", 10), Choice("15 Puntos", 15), Choice("20 Puntos", 20)] int puntos)
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

        if (_gestorPartidas.TieneRetoPendiente(retadorId))
        {
            await RespondAsync("⚠️ Ya tenés un reto pendiente. Cancelalo o esperá a que se resuelva.", ephemeral: true);
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
            .WithButton("✅ Aceptar", $"reto_aceptar_{retadorId}_{retadoId}_{apuesta}_{puntos}", ButtonStyle.Success)
            .WithButton("❌ Cancelar", $"reto_cancelar_{retadorId}_{retadoId}_{apuesta}_{puntos}", ButtonStyle.Danger)
            .Build();

        await RespondAsync(
            $"⚔️ ¡El **{usuarioRetador.TituloEquipado ?? "Jugador"}** {Context.User.Mention} desafía a {usuario.Mention} a una partida de Truco a {puntos} puntos por 🪙 {apuesta} monedas! (expira en 30s)",
            components: componentes);

        var mensaje = await GetOriginalResponseAsync();
        _gestorPartidas.RegistrarReto(new RetoPendiente(retadorId, retadoId, apuesta, puntos, Context.Channel.Id, mensaje.Id));
    }

    [ComponentInteraction("reto_aceptar_*_*_*_*")]
    public async Task AceptarReto(ulong retadorId, ulong retadoId, int apuesta, int puntos)
    {
        if (Context.User.Id != retadoId)
        {
            await RespondAsync("🚫 Solo el retado puede aceptar.", ephemeral: true);
            return;
        }

        if (!_gestorPartidas.TryQuitarReto(retadorId, out _))
        {
            await RespondAsync("⚠️ Este reto ya no está disponible.", ephemeral: true);
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

        _gestorPartidas.IniciarPartida(Context.Channel.Id, retadorId, retadoId, puntos);
        _gestorPartidas.ApuestasActivas[Context.Channel.Id] = apuesta;
        var ronda = _gestorPartidas.ObtenerPartidaPorUsuario(retadoId)!;

        await ((SocketMessageComponent)Context.Interaction).UpdateAsync(mensaje => mensaje.Components = new ComponentBuilder().Build());

        await using var streamMesa = await _generadorImagenes.GenerarMesaActualAsync(ronda.Muestra, null, null);
        await Context.Channel.SendFileAsync(
            streamMesa,
            "mesa.png",
            text: $"{GenerarTextoMarcador(ronda)}🎉 ¡Partida a {puntos} puntos iniciada por 🪙 {apuesta} monedas! 🃏 La muestra es **{ronda.Muestra}**.",
            components: ConstruirBotonesDeAccion(ronda));
    }

    [ComponentInteraction("reto_cancelar_*_*_*_*")]
    public async Task CancelarReto(ulong retadorId, ulong retadoId, int apuesta, int puntos)
    {
        if (Context.User.Id != retadorId)
        {
            await RespondAsync("🚫 Solo quien retó puede cancelar el reto.", ephemeral: true);
            return;
        }

        if (!_gestorPartidas.TryQuitarReto(retadorId, out _))
        {
            await RespondAsync("⚠️ Este reto ya no está disponible.", ephemeral: true);
            return;
        }

        await ((SocketMessageComponent)Context.Interaction).UpdateAsync(mensaje =>
        {
            mensaje.Content = $"❌ *Reto cancelado por <@{retadorId}>.*";
            mensaje.Components = new ComponentBuilder().Build();
        });
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

        componentes.WithButton("📊 Orden de las cartas", "ayuda_cartas", ButtonStyle.Secondary, row: 4);

        var turnoTexto = ronda.TurnoActual == userId ? "✅ Es tu turno." : $"⏳ Turno de <@{ronda.TurnoActual}>.";

        await using var streamMano = await _generadorImagenes.GenerarManoAsync(mano);
        await RespondWithFileAsync(streamMano, "mano.png", text: turnoTexto, components: componentes.Build(), ephemeral: true);
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
        var jugadorQueJuega = Context.User.Id;
        var faseAntes = ronda.Fase;
        var muestraAntes = ronda.Muestra;

        try
        {
            ronda.JugarCarta(jugadorQueJuega, carta);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        var seResolvioLaBaza = ronda.Fase != faseAntes;
        var arrancoManoNueva = ronda.Muestra != muestraAntes;
        var partidaTerminada = ronda.Fase == FaseRonda.Finalizada;

        // Paso A (siempre): la mesa tal cual queda justo despues de que esta carta cae. Si
        // la baza se resolvio, usamos UltimaCartaMesaJ1/J2 (Ronda las guarda ANTES de
        // limpiar/repartir de nuevo) en vez de las listas acumuladas: esas pueden haber
        // quedado vacias si con esta jugada ya arranco la mano siguiente.
        Carta? jugada1Mesa;
        Carta? jugada2Mesa;
        if (seResolvioLaBaza)
        {
            jugada1Mesa = ronda.UltimaCartaMesaJ1;
            jugada2Mesa = ronda.UltimaCartaMesaJ2;
        }
        else
        {
            jugada1Mesa = jugadorQueJuega == ronda.Jugador1Id ? carta : null;
            jugada2Mesa = jugadorQueJuega == ronda.Jugador2Id ? carta : null;
        }

        var textoJugada = $"🎴 Jugador <@{jugadorQueJuega}> jugó **{carta}**.";
        if (seResolvioLaBaza && ronda.GanadorUltimaMano is not null)
        {
            textoJugada += $" ¡<@{ronda.GanadorUltimaMano}> ganó la mano!";
        }

        await using (var streamMesa = await _generadorImagenes.GenerarMesaActualAsync(muestraAntes, jugada1Mesa, jugada2Mesa))
        {
            await Context.Channel.SendFileAsync(
                streamMesa,
                "mesa.png",
                text: $"{GenerarTextoMarcador(ronda)}{textoJugada}",
                components: (arrancoManoNueva || partidaTerminada) ? null : ConstruirBotonesDeAccion(ronda));
        }

        if (partidaTerminada)
        {
            await FinalizarYAnunciarRonda(ronda);
        }
        else if (arrancoManoNueva)
        {
            // Paso B: se gano la mano anterior pero nadie llego al PuntosObjetivo, asi que
            // ya arranco la mano siguiente con cartas y muestra nuevas.
            await EnviarNuevaRondaAsync(ronda);
        }

        await DeferAsync();
    }

    [ComponentInteraction("seleccionar_envido")]
    public async Task SeleccionarEnvido(string[] opciones)
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        if (opciones[0] == "flor")
        {
            try
            {
                ronda.CantarFlor(Context.User.Id);
            }
            catch (InvalidOperationException ex)
            {
                await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
                return;
            }

            if (ronda.Estado == EstadoRonda.RespondiendoFlor)
            {
                await Context.Channel.SendMessageAsync(
                    $"{GenerarTextoMarcador(ronda)}🌸 ¡<@{Context.User.Id}> cantó Flor! Pero huele a jardín... <@{ronda.TurnoActual}>, ¿qué respondés?",
                    components: ConstruirBotonesDeAccion(ronda));
                await DeferAsync();
                return;
            }

            var partidaTerminada = ronda.Fase == FaseRonda.Finalizada;
            var textoFlor = partidaTerminada
                ? $"🌸 ¡<@{Context.User.Id}> cantó FLOR ({ronda.CalcularPuntosFlor(Context.User.Id)} puntos)!"
                : $"🌸 ¡<@{Context.User.Id}> cantó Flor (3 pts)! El Envido se anula. Turno de jugar carta para <@{ronda.TurnoActual}>.";

            if (partidaTerminada)
            {
                await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{textoFlor}");
                await FinalizarYAnunciarRonda(ronda);
            }
            else
            {
                await Context.Channel.SendMessageAsync(
                    $"{GenerarTextoMarcador(ronda)}{textoFlor}",
                    components: ConstruirBotonesDeAccion(ronda));
            }

            await DeferAsync();
            return;
        }

        var (tipoEnvido, nombreEnvido) = opciones[0] switch
        {
            "envido" => (Canto.Envido, "Envido"),
            "real_envido" => (Canto.RealEnvido, "Real Envido"),
            "falta_envido" => (Canto.FaltaEnvido, "Falta Envido"),
            _ => throw new ArgumentOutOfRangeException(nameof(opciones), "Opcion de envido desconocida."),
        };

        try
        {
            ronda.CantarEnvido(Context.User.Id, tipoEnvido);
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

        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}🎲 ¡<@{Context.User.Id}> cantó {nombreEnvido}!",
            components: botones);
        await DeferAsync();
    }

    [ComponentInteraction("respuesta_flor")]
    public async Task RespuestaFlor(string[] opciones)
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        var puntosJugador1Antes = ronda.PuntosJugador1;
        var puntosJugador2Antes = ronda.PuntosJugador2;

        try
        {
            ronda.ResponderFlor(Context.User.Id, opciones[0]);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        if (ronda.Estado == EstadoRonda.RespondiendoContraFlor)
        {
            await Context.Channel.SendMessageAsync(
                $"{GenerarTextoMarcador(ronda)}🔥 ¡<@{Context.User.Id}> retrucó la Flor! Turno de <@{ronda.TurnoActual}>.",
                components: ConstruirBotonesDeAccion(ronda));
            await DeferAsync();
            return;
        }

        var deltaJugador1 = ronda.PuntosJugador1 - puntosJugador1Antes;
        var ganador = deltaJugador1 > 0 ? ronda.Jugador1Id : ronda.Jugador2Id;
        var puntosGanados = deltaJugador1 > 0 ? deltaJugador1 : ronda.PuntosJugador2 - puntosJugador2Antes;
        var textoResultado = $"🌸 ¡<@{ganador}> gana el cruce de Flores y se lleva {puntosGanados} puntos!";

        if (ronda.Fase == FaseRonda.Finalizada)
        {
            await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{textoResultado}");
            await FinalizarYAnunciarRonda(ronda);
        }
        else
        {
            await Context.Channel.SendMessageAsync(
                $"{GenerarTextoMarcador(ronda)}{textoResultado}",
                components: ConstruirBotonesDeAccion(ronda));
        }

        await DeferAsync();
    }

    [ComponentInteraction("contraflor_*")]
    public async Task ResponderContraFlor(string accion)
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        var quiere = accion == "quiero";
        var puntosJugador1Antes = ronda.PuntosJugador1;
        var puntosJugador2Antes = ronda.PuntosJugador2;

        try
        {
            ronda.ResponderContraFlor(Context.User.Id, quiere);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        var deltaJugador1 = ronda.PuntosJugador1 - puntosJugador1Antes;
        var ganador = deltaJugador1 > 0 ? ronda.Jugador1Id : ronda.Jugador2Id;
        var puntosGanados = deltaJugador1 > 0 ? deltaJugador1 : ronda.PuntosJugador2 - puntosJugador2Antes;

        var textoResultado = quiere
            ? $"🌸 ¡<@{ganador}> gana el cruce de Flores y se lleva {puntosGanados} puntos!"
            : $"❌ <@{Context.User.Id}> no quiso. <@{ganador}> se lleva {puntosGanados} puntos de Flor.";

        if (ronda.Fase == FaseRonda.Finalizada)
        {
            await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{textoResultado}");
            await FinalizarYAnunciarRonda(ronda);
        }
        else
        {
            await Context.Channel.SendMessageAsync(
                $"{GenerarTextoMarcador(ronda)}{textoResultado}",
                components: ConstruirBotonesDeAccion(ronda));
        }

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

        // Si ya hay un truco pendiente de respuesta, este click es una escalada "tipo tenis"
        // (ej. el rival responde Retruco directo en vez de Quiero) — el siguiente nivel se
        // calcula sobre lo pendiente, no sobre ValorTrucoActual (que todavia no se actualizo).
        var siguienteCanto = ronda.CantoTrucoPendiente is not null
            ? SiguienteEscalada(ronda.CantoTrucoPendiente.Value)
            : SiguienteCantoTruco(ronda.ValorTrucoActual);

        if (siguienteCanto is null)
        {
            await RespondAsync("⚠️ Ya estas jugando vale 4.", ephemeral: true);
            return;
        }

        var canto = siguienteCanto.Value;

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
            $"{GenerarTextoMarcador(ronda)}🔥 <@{Context.User.Id}> gritó **{NombreCantoTruco(canto)}**!",
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

        if (ronda.Fase == FaseRonda.Finalizada)
        {
            await FinalizarYAnunciarRonda(ronda);
        }
        else
        {
            await EnviarNuevaRondaAsync(ronda);
        }

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

        var cantador = ronda.JugadorQueCanto!.Value;
        var quienResponde = cantador == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;

        if (Context.User.Id != quienResponde)
        {
            await RespondAsync("🚫 No sos quien tiene que responder este canto.", ephemeral: true);
            return;
        }

        var respuesta = accion == "quiero" ? RespuestaCanto.Quiero : RespuestaCanto.NoQuiero;
        var puntosJugador1Antes = ronda.PuntosJugador1;
        var puntosJugador2Antes = ronda.PuntosJugador2;

        try
        {
            ronda.ResponderEnvido(Context.User.Id, respuesta);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        var deltaJugador1 = ronda.PuntosJugador1 - puntosJugador1Antes;
        var ganador = deltaJugador1 > 0 ? ronda.Jugador1Id : ronda.Jugador2Id;
        var puntosGanados = deltaJugador1 > 0 ? deltaJugador1 : ronda.PuntosJugador2 - puntosJugador2Antes;

        string mensajePuntos;
        if (respuesta == RespuestaCanto.NoQuiero)
        {
            mensajePuntos = $"❌ <@{quienResponde}> no quiso. <@{ganador}> se lleva {puntosGanados} punto(s) de envido.";
        }
        else
        {
            var puntos1 = ronda.CalcularEnvido(ronda.Jugador1Id);
            var puntos2 = ronda.CalcularEnvido(ronda.Jugador2Id);
            mensajePuntos = $"🎲 <@{ganador}> se lleva {puntosGanados} puntos de envido. Tantos: <@{ronda.Jugador1Id}> {puntos1} | <@{ronda.Jugador2Id}> {puntos2}.";
        }

        if (ronda.Fase == FaseRonda.Finalizada)
        {
            await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{mensajePuntos}");
            await FinalizarYAnunciarRonda(ronda);
        }
        else
        {
            await Context.Channel.SendMessageAsync(
                $"{GenerarTextoMarcador(ronda)}{mensajePuntos}",
                components: ConstruirBotonesDeAccion(ronda));
        }

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
        var muestraAntes = ronda.Muestra;

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
            await FinalizarYAnunciarRonda(ronda);
        }
        else if (ronda.Muestra != muestraAntes)
        {
            // Se rechazo el truco: se gano esta mano pero nadie llego al PuntosObjetivo,
            // asi que ya arranco la mano siguiente con cartas nuevas.
            await EnviarNuevaRondaAsync(ronda);
        }
        else
        {
            await Context.Channel.SendMessageAsync(
                $"{GenerarTextoMarcador(ronda)}🔥 ¡Truco por {ronda.ValorTrucoActual}!",
                components: ConstruirBotonesDeAccion(ronda));
        }

        await DeferAsync();
    }

    private async Task EnviarNuevaRondaAsync(Ronda ronda)
    {
        await using var streamMesaNueva = await _generadorImagenes.GenerarMesaActualAsync(ronda.Muestra, null, null);
        await Context.Channel.SendFileAsync(
            streamMesaNueva,
            "mesa.png",
            text: $"{GenerarTextoMarcador(ronda)}🔄 Nueva ronda, reparte las cartas... La nueva muestra es **{ronda.Muestra}**.",
            components: ConstruirBotonesDeAccion(ronda));
    }

    private async Task FinalizarYAnunciarRonda(Ronda ronda)
    {
        var ganadorId = ronda.GanadorRonda!.Value;
        var perdedorId = ganadorId == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;
        var apuesta = _gestorPartidas.ApuestasActivas.GetValueOrDefault(Context.Channel.Id);
        var pozo = apuesta * 2;

        await _usuarioRepository.ActualizarMonedasAsync(ganadorId, pozo);
        await _usuarioRepository.RegistrarPartidaAsync(ganadorId, perdedorId, apuesta);
        await _usuarioRepository.SumarVictoriaAsync(ganadorId);
        await _usuarioRepository.SumarDerrotaAsync(perdedorId);

        var (subioGanador, nivelGanador) = await _usuarioRepository.SumarExpAsync(ganadorId, 50);
        var (subioPerdedor, nivelPerdedor) = await _usuarioRepository.SumarExpAsync(perdedorId, 15);

        _gestorPartidas.FinalizarPartida(Context.Channel.Id);

        await Context.Channel.SendMessageAsync(
            $"🏆 ¡Ronda finalizada! <@{ganadorId}> gana la partida y se lleva 🪙 {pozo} monedas!");

        if (subioGanador)
        {
            await Context.Channel.SendMessageAsync($"🎉 ¡Felicidades <@{ganadorId}>! Has alcanzado el **Nivel {nivelGanador}**.");
        }

        if (subioPerdedor)
        {
            await Context.Channel.SendMessageAsync($"🎉 ¡Felicidades <@{perdedorId}>! Has alcanzado el **Nivel {nivelPerdedor}**.");
        }
    }

    private static MessageComponent ConstruirBotonesDeAccion(Ronda ronda)
    {
        var botones = new ComponentBuilder()
            .WithButton("🃏 Ver mis cartas", "ver_mano", ButtonStyle.Primary, row: 0);

        if (ronda.Estado == EstadoRonda.RespondiendoFlor)
        {
            var selectFlor = new SelectMenuBuilder()
                .WithCustomId("respuesta_flor")
                .WithPlaceholder("🌸 Responder a la Flor...")
                .AddOption("🌸 La mía es Flor", "la_mia_es_flor")
                .AddOption("🔥 Con Flor Envido", "con_flor_envido")
                .AddOption("☠️ Contra Flor al Resto", "contra_flor_al_resto");

            botones.WithSelectMenu(selectFlor, row: 1);
        }
        else if (ronda.Estado == EstadoRonda.RespondiendoContraFlor)
        {
            botones.WithButton("✅ Quiero", "contraflor_quiero", ButtonStyle.Success, row: 1);
            botones.WithButton("❌ No Quiero", "contraflor_noquiero", ButtonStyle.Danger, row: 1);
        }
        else if (ronda.Estado == EstadoRonda.EsperandoEnvido || ronda.Estado == EstadoRonda.JugandoCartas)
        {
            if (ronda.PuedeCantarEnvido(ronda.TurnoActual))
            {
                // Los select menus de Discord necesitan su propia fila (no pueden compartirla con botones).
                var selectEnvido = new SelectMenuBuilder()
                    .WithCustomId("seleccionar_envido")
                    .WithPlaceholder("🎲 Cantar Envido...")
                    .AddOption("Envido", "envido")
                    .AddOption("Real Envido", "real_envido")
                    .AddOption("Falta Envido", "falta_envido")
                    .AddOption("🌸 Flor", "flor");

                botones.WithSelectMenu(selectEnvido, row: 1);
            }

            if (ronda.ValorTrucoActual < 4)
            {
                botones.WithButton(
                    $"🔥 {NombreCantoTruco(SiguienteCantoTruco(ronda.ValorTrucoActual))}",
                    "gritar_truco",
                    ButtonStyle.Secondary,
                    row: 2);
            }

            botones.WithButton("🏳️ Irse al Mazo", "irse_mazo", ButtonStyle.Danger, row: 2);
        }

        return botones.Build();
    }

    private static string GenerarPalitos(int puntos)
    {
        var cuadrados = puntos / 5;
        var resto = puntos % 5;
        var palitos = string.Concat(Enumerable.Repeat("[X] ", cuadrados));
        palitos += resto switch
        {
            1 => "|",
            2 => "||",
            3 => "|||",
            4 => "||||",
            _ => "",
        };
        return palitos.TrimEnd();
    }

    private static string GenerarTextoMarcador(Ronda ronda)
    {
        var j1 = $"<@{ronda.Jugador1Id}>: {ronda.PuntosJugador1} {GenerarPalitos(ronda.PuntosJugador1)}";
        var j2 = $"<@{ronda.Jugador2Id}>: {ronda.PuntosJugador2} {GenerarPalitos(ronda.PuntosJugador2)}";
        var turno = ronda.Fase == FaseRonda.Finalizada ? "" : $"👉 Turno de <@{ronda.TurnoActual}>\n";
        return $"**MARCADOR** (A {ronda.PuntosObjetivo})\n{j1}\n{j2}\n{turno}\n";
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
