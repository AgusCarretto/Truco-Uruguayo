namespace TrucoUruguayo.Bot.Modelo;

public class ItemInventario
{
    public int ItemId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateTime FechaCompra { get; set; }
    public bool Equipado { get; set; }
}
