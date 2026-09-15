namespace TrucoUruguayo.Bot.Modelo;

public static class NivelCalculadora
{
    public static int XpParaAlcanzarNivel(int nivel) => 100 * (nivel - 1) * nivel / 2;
}
