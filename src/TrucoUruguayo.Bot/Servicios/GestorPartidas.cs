using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Core.Juego;

namespace TrucoUruguayo.Bot.Servicios;

public class GestorPartidas
{
    private static readonly TimeSpan IntervaloChequeoAfk = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LimiteInactividad = TimeSpan.FromMinutes(1);

    private readonly DiscordSocketClient _client;
    private readonly UsuarioRepository _usuarioRepository;
    private readonly Timer _timerAfk;
    private readonly TimeSpan _limiteReto;

    public ConcurrentDictionary<ulong, Ronda> PartidasActivas { get; } = new();
    public ConcurrentDictionary<ulong, ulong> JugadoresActivos { get; } = new();
    public ConcurrentDictionary<ulong, int> ApuestasActivas { get; } = new();
    public ConcurrentDictionary<ulong, RetoPendiente> RetosPendientes { get; } = new();

    public GestorPartidas(DiscordSocketClient client, UsuarioRepository usuarioRepository, TimeSpan? limiteReto = null)
    {
        _client = client;
        _usuarioRepository = usuarioRepository;
        _limiteReto = limiteReto ?? TimeSpan.FromSeconds(30);
        _timerAfk = new Timer(ChequearInactividadAsync, null, IntervaloChequeoAfk, IntervaloChequeoAfk);
    }

    public bool IniciarPartida(ulong canalId, ulong jugador1Id, ulong jugador2Id, int puntosObjetivo)
    {
        if (!JugadoresActivos.TryAdd(jugador1Id, canalId))
        {
            return false;
        }

        if (!JugadoresActivos.TryAdd(jugador2Id, canalId))
        {
            JugadoresActivos.TryRemove(jugador1Id, out _);
            return false;
        }

        PartidasActivas[canalId] = new Ronda(jugador1Id, jugador2Id, puntosObjetivo);
        return true;
    }

    public Ronda? ObtenerPartidaPorUsuario(ulong userId)
    {
        if (!JugadoresActivos.TryGetValue(userId, out var canalId))
        {
            return null;
        }

        return PartidasActivas.TryGetValue(canalId, out var ronda) ? ronda : null;
    }

    public async Task FinalizarPartidaAsync(ulong canalId, ulong ganadorId)
    {
        if (!PartidasActivas.TryRemove(canalId, out var ronda))
        {
            return;
        }

        JugadoresActivos.TryRemove(ronda.Jugador1Id, out _);
        JugadoresActivos.TryRemove(ronda.Jugador2Id, out _);
        ApuestasActivas.TryRemove(canalId, out _);

        var logros = await _usuarioRepository.RegistrarProgresoAsync(ganadorId, "partidas_ganadas");
        if (logros.Count > 0 && _client.GetChannel(canalId) is IMessageChannel canal)
        {
            foreach (var logro in logros)
            {
                await canal.SendMessageAsync(
                    $"🎉 ¡<@{ganadorId}> desbloqueó el logro **{logro.Nombre}** {logro.Emoji} y ganó {logro.RecompensaMonedas} monedas!");
            }
        }
    }

    // Se llama al cerrar el bot de forma prolija (Ctrl+C o SIGTERM de Docker, ver
    // Program.cs). No persistimos las partidas en curso, asi que en vez de intentar
    // retomarlas al arrancar de nuevo, se liquidan ahora mismo: nadie pierde su apuesta (ya
    // no se descuenta hasta el final, ver FinalizarPartidaAsync/ChequearInactividadAsync) y
    // se le suma a cada uno un extra como disculpa por el corte.
    public async Task CompensarPartidasActivasPorCierreAsync()
    {
        // Frena el timer de AFK y espera a que termine si estaba a mitad de liquidar algo,
        // para no liquidar la misma partida dos veces en paralelo.
        await _timerAfk.DisposeAsync();

        foreach (var (canalId, ronda) in PartidasActivas.ToArray())
        {
            var apuesta = ApuestasActivas.GetValueOrDefault(canalId);

            if (apuesta > 0)
            {
                await _usuarioRepository.ActualizarMonedasAsync(ronda.Jugador1Id, apuesta);
                await _usuarioRepository.ActualizarMonedasAsync(ronda.Jugador2Id, apuesta);
            }

            if (_client.GetChannel(canalId) is IMessageChannel canal)
            {
                try
                {
                    var textoExtra = apuesta > 0
                        ? $", y les sumamos 🪙 {apuesta} monedas extra a cada uno como disculpa"
                        : string.Empty;

                    await canal.SendMessageAsync(
                        $"🔧 El bot se tiene que reiniciar y esta partida queda cortada acá. "
                        + $"No perdiste nada de tu apuesta{textoExtra}. ¡Empezá una nueva partida cuando quieras!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"No se pudo avisar en el canal {canalId} sobre el cierre: {ex.Message}");
                }
            }

            PartidasActivas.TryRemove(canalId, out _);
            JugadoresActivos.TryRemove(ronda.Jugador1Id, out _);
            JugadoresActivos.TryRemove(ronda.Jugador2Id, out _);
            ApuestasActivas.TryRemove(canalId, out _);
        }
    }

    public bool TieneRetoPendiente(ulong retadorId) => RetosPendientes.ContainsKey(retadorId);

    public bool RegistrarReto(RetoPendiente reto)
    {
        if (!RetosPendientes.TryAdd(reto.RetadorId, reto))
        {
            return false;
        }

        reto.TimerExpiracion = new Timer(ExpirarRetoAsync, reto.RetadorId, _limiteReto, Timeout.InfiniteTimeSpan);
        return true;
    }

    public bool TryQuitarReto(ulong retadorId, out RetoPendiente? reto)
    {
        if (!RetosPendientes.TryRemove(retadorId, out reto))
        {
            return false;
        }

        reto.TimerExpiracion?.Dispose();
        return true;
    }

    private async void ExpirarRetoAsync(object? state)
    {
        var retadorId = (ulong)state!;

        if (!TryQuitarReto(retadorId, out var reto) || reto is null)
        {
            return;
        }

        try
        {
            if (_client.GetChannel(reto.CanalId) is IMessageChannel canal)
            {
                await canal.ModifyMessageAsync(reto.MensajeId, mensaje =>
                {
                    mensaje.Content = $"⌛ El reto de <@{reto.RetadorId}> a <@{reto.RetadoId}> expiró (no se aceptó a tiempo).";
                    mensaje.Components = new ComponentBuilder().Build();
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error expirando reto: {ex.Message}");
        }
    }

    private async void ChequearInactividadAsync(object? state)
    {
        try
        {
            foreach (var (canalId, ronda) in PartidasActivas.ToArray())
            {
                if (DateTime.UtcNow - ronda.UltimaActividad <= LimiteInactividad)
                {
                    continue;
                }

                var afkId = ronda.TurnoActual;
                var ganadorId = afkId == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;
                var apuesta = ApuestasActivas.GetValueOrDefault(canalId);

                await _usuarioRepository.ActualizarMonedasAsync(ganadorId, apuesta);
                await _usuarioRepository.ActualizarMonedasAsync(afkId, -apuesta);
                await _usuarioRepository.RegistrarPartidaAsync(ganadorId, afkId, apuesta);
                await _usuarioRepository.SumarVictoriaAsync(ganadorId);
                await _usuarioRepository.SumarDerrotaAsync(afkId);

                var (subioGanador, nivelGanador) = await _usuarioRepository.SumarExpAsync(ganadorId, 50);
                var (subioPerdedor, nivelPerdedor) = await _usuarioRepository.SumarExpAsync(afkId, 15);

                await FinalizarPartidaAsync(canalId, ganadorId);

                if (_client.GetChannel(canalId) is IMessageChannel canal)
                {
                    await canal.SendMessageAsync(
                        $"⏳ ¡<@{afkId}> se quedó dormido (AFK)! <@{ganadorId}> gana por abandono y se lleva 🪙 {apuesta} monedas.");

                    if (subioGanador)
                    {
                        await canal.SendMessageAsync($"🎉 ¡Felicidades <@{ganadorId}>! Has alcanzado el **Nivel {nivelGanador}**.");
                    }

                    if (subioPerdedor)
                    {
                        await canal.SendMessageAsync($"🎉 ¡Felicidades <@{afkId}>! Has alcanzado el **Nivel {nivelPerdedor}**.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error chequeando inactividad: {ex.Message}");
        }
    }
}
