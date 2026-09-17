namespace TrucoUruguayo.Bot.Modelo;

public class Logro
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string EstadisticaClave { get; set; } = string.Empty;
    public int Meta { get; set; }
    public int RecompensaMonedas { get; set; }
    public string Emoji { get; set; } = string.Empty;
}
