namespace TrucoUruguayo.Bot.Modelo;

public class PartidaHistorico
{
    public int Id { get; set; }
    public long GanadorId { get; set; }
    public long PerdedorId { get; set; }
    public int Apuesta { get; set; }
    public DateTime Fecha { get; set; }
}
