namespace TrucoUruguayo.Bot.Servicios;

public class RetoPendiente
{
    public ulong RetadorId { get; }
    public ulong RetadoId { get; }
    public int Apuesta { get; }
    public int Puntos { get; }
    public ulong CanalId { get; }
    public ulong MensajeId { get; }
    public Timer? TimerExpiracion { get; set; }

    public RetoPendiente(ulong retadorId, ulong retadoId, int apuesta, int puntos, ulong canalId, ulong mensajeId)
    {
        RetadorId = retadorId;
        RetadoId = retadoId;
        Apuesta = apuesta;
        Puntos = puntos;
        CanalId = canalId;
        MensajeId = mensajeId;
    }
}
