using System.Collections.Generic;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Juego;

public sealed record Reparto(Carta Muestra, List<Carta> ManoJugador1, List<Carta> ManoJugador2);
