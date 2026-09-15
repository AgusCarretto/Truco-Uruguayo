namespace TrucoUruguayo.Bot.Modelo;

public class Usuario
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Monedas { get; set; }
    public int Victorias { get; set; }
    public int Derrotas { get; set; }
    public int Xp { get; set; }
    public int Nivel { get; set; }
    public string? TituloEquipado { get; set; }
}
