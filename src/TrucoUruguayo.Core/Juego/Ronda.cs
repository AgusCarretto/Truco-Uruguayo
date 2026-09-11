using System.Collections.Generic;
using TrucoUruguayo.Core.Jerarquia;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Juego;

public class Ronda
{
    public ulong Jugador1Id { get; }
    public ulong Jugador2Id { get; }
    public Carta Muestra { get; }
    public List<Carta> ManoJugador1 { get; }
    public List<Carta> ManoJugador2 { get; }
    public GestorDeJerarquia Gestor { get; }
    public EstadoRonda Estado { get; private set; }
    public ulong TurnoActual { get; private set; }

    public Ronda(ulong jugador1Id, ulong jugador2Id)
    {
        Jugador1Id = jugador1Id;
        Jugador2Id = jugador2Id;

        var mazo = new Mazo();
        mazo.Mezclar();
        var reparto = mazo.Repartir();

        Muestra = reparto.Muestra;
        ManoJugador1 = reparto.ManoJugador1;
        ManoJugador2 = reparto.ManoJugador2;
        Gestor = new GestorDeJerarquia(Muestra);

        Estado = EstadoRonda.EsperandoEnvido;
        TurnoActual = jugador1Id;
    }
}
