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

    public ConcurrentDictionary<ulong, Ronda> PartidasActivas { get; } = new();
    public ConcurrentDictionary<ulong, ulong> JugadoresActivos { get; } = new();
    public ConcurrentDictionary<ulong, int> ApuestasActivas { get; } = new();

    public GestorPartidas(DiscordSocketClient client, UsuarioRepository usuarioRepository)
    {
        _client = client;
        _usuarioRepository = usuarioRepository;
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

    public void FinalizarPartida(ulong canalId)
    {
        if (!PartidasActivas.TryRemove(canalId, out var ronda))
        {
            return;
        }

        JugadoresActivos.TryRemove(ronda.Jugador1Id, out _);
        JugadoresActivos.TryRemove(ronda.Jugador2Id, out _);
        ApuestasActivas.TryRemove(canalId, out _);
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
                var pozo = apuesta * 2;

                await _usuarioRepository.ActualizarMonedasAsync(ganadorId, pozo);
                await _usuarioRepository.RegistrarPartidaAsync(ganadorId, afkId, apuesta);
                await _usuarioRepository.SumarVictoriaAsync(ganadorId);
                await _usuarioRepository.SumarDerrotaAsync(afkId);

                var (subioGanador, nivelGanador) = await _usuarioRepository.SumarExpAsync(ganadorId, 50);
                var (subioPerdedor, nivelPerdedor) = await _usuarioRepository.SumarExpAsync(afkId, 15);

                FinalizarPartida(canalId);

                if (_client.GetChannel(canalId) is IMessageChannel canal)
                {
                    await canal.SendMessageAsync(
                        $"⏳ ¡<@{afkId}> se quedó dormido (AFK)! <@{ganadorId}> gana por abandono y se lleva el pozo.");

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
