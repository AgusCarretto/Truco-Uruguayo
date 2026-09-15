using System;
using System.Collections.Generic;
using System.Linq;
using TrucoUruguayo.Core.Jerarquia;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Juego;

public class Ronda
{
    private enum ResultadoMano
    {
        Jugador1,
        Jugador2,
        Parda,
    }

    private readonly List<Carta> _manoJugador1;
    private readonly List<Carta> _manoJugador2;

    // El envido se puede cantar/responder incluso despues de que alguno de los dos ya
    // jugo una carta de esta mano (ver PuedeCantarEnvido). Para eso el calculo del
    // envido tiene que usar SIEMPRE la mano original de 3 cartas de esta mano, no
    // _manoJugador1/2 (que van perdiendo cartas a medida que se juegan).
    private Carta[] _manoOriginalJugador1;
    private Carta[] _manoOriginalJugador2;

    private readonly List<Carta> _cartasJugadasJugador1 = new();
    private readonly List<Carta> _cartasJugadasJugador2 = new();
    private readonly List<ResultadoMano> _resultadosManos = new();

    private Carta? _cartaActualJugador1;
    private Carta? _cartaActualJugador2;
    private Canto? _cantoPendiente;
    private ulong _turnoAntesDelCanto;
    private CantoTruco? _cantoTrucoPendiente;
    private EstadoRonda _estadoAntesDelTruco;
    private ulong _turnoAntesDelTruco;

    public ulong Jugador1Id { get; }
    public ulong Jugador2Id { get; }
    public Carta Muestra { get; private set; }
    public IReadOnlyList<Carta> ManoJugador1 => _manoJugador1;
    public IReadOnlyList<Carta> ManoJugador2 => _manoJugador2;
    public IReadOnlyList<Carta> CartasJugadasJugador1 => _cartasJugadasJugador1;
    public IReadOnlyList<Carta> CartasJugadasJugador2 => _cartasJugadasJugador2;
    public GestorDeJerarquia Gestor { get; private set; }
    public EstadoRonda Estado { get; private set; }
    public FaseRonda Fase { get; private set; }
    public ulong TurnoActual { get; private set; }
    public ulong JugadorManoId { get; private set; }
    public ulong? GanadorRonda { get; private set; }
    public int PuntosJugador1 { get; private set; }
    public int PuntosJugador2 { get; private set; }
    public int PuntosObjetivo { get; private set; }
    public ulong? JugadorQueCanto { get; private set; }
    public Canto? EnvidoActual => _cantoPendiente;
    public bool EnvidoCantado { get; private set; }
    public int ValorTrucoActual { get; private set; } = 1;
    public ulong? TurnoCantoTruco { get; private set; }
    public ulong? JugadorQueGritoTruco { get; private set; }
    public CantoTruco? CantoTrucoPendiente => _cantoTrucoPendiente;
    public DateTime UltimaActividad { get; private set; } = DateTime.UtcNow;
    public Carta? UltimaCartaMesaJ1 { get; private set; }
    public Carta? UltimaCartaMesaJ2 { get; private set; }
    public ulong? GanadorUltimaMano { get; private set; }
    public Dictionary<ulong, bool> FlorCantada { get; private set; }

    public Ronda(ulong jugador1Id, ulong jugador2Id, int puntosObjetivo)
    {
        var mazo = new Mazo();
        mazo.Mezclar();
        var reparto = mazo.Repartir();

        Jugador1Id = jugador1Id;
        Jugador2Id = jugador2Id;
        Muestra = reparto.Muestra;
        _manoJugador1 = reparto.ManoJugador1;
        _manoJugador2 = reparto.ManoJugador2;
        _manoOriginalJugador1 = _manoJugador1.ToArray();
        _manoOriginalJugador2 = _manoJugador2.ToArray();
        Gestor = new GestorDeJerarquia(Muestra);
        PuntosObjetivo = puntosObjetivo;
        JugadorManoId = Random.Shared.Next(2) == 0 ? jugador1Id : jugador2Id;

        Estado = EstadoRonda.EsperandoEnvido;
        Fase = FaseRonda.PrimeraMano;
        TurnoActual = JugadorManoId;
        FlorCantada = new();
    }

    internal Ronda(ulong jugador1Id, ulong jugador2Id, Carta muestra, List<Carta> manoJugador1, List<Carta> manoJugador2, int puntosObjetivo, ulong jugadorManoId)
    {
        Jugador1Id = jugador1Id;
        Jugador2Id = jugador2Id;
        Muestra = muestra;
        _manoJugador1 = manoJugador1;
        _manoJugador2 = manoJugador2;
        _manoOriginalJugador1 = _manoJugador1.ToArray();
        _manoOriginalJugador2 = _manoJugador2.ToArray();
        Gestor = new GestorDeJerarquia(Muestra);
        PuntosObjetivo = puntosObjetivo;
        JugadorManoId = jugadorManoId;

        Estado = EstadoRonda.EsperandoEnvido;
        Fase = FaseRonda.PrimeraMano;
        TurnoActual = JugadorManoId;
        FlorCantada = new();
    }

    public void CantarEnvido(ulong jugadorId, Canto canto)
    {
        if (jugadorId != TurnoActual)
        {
            throw new InvalidOperationException("Solo podés cantar envido en tu turno.");
        }

        if (Estado == EstadoRonda.RespondiendoCanto || Estado == EstadoRonda.RespondiendoTruco)
        {
            throw new InvalidOperationException("Hay un canto pendiente de respuesta.");
        }

        // El envido se puede cantar hasta que ese jugador juegue su primera carta de la
        // mano (aunque el rival ya haya jugado la suya), no solo mientras Estado siga en
        // EsperandoEnvido a nivel global.
        if (!PuedeCantarEnvido(jugadorId))
        {
            throw new InvalidOperationException("No se puede cantar un envido en este momento.");
        }

        EnvidoCantado = true;
        _cantoPendiente = canto;
        JugadorQueCanto = jugadorId;
        _turnoAntesDelCanto = TurnoActual;
        Estado = EstadoRonda.RespondiendoCanto;
        TurnoActual = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;
        RegistrarActividad();
    }

    public void ResponderEnvido(ulong jugadorId, RespuestaCanto respuesta)
    {
        if (jugadorId != TurnoActual)
        {
            throw new InvalidOperationException("No es el turno de este jugador.");
        }

        if (Estado != EstadoRonda.RespondiendoCanto)
        {
            throw new InvalidOperationException("No hay ningun canto pendiente para responder.");
        }

        var canto = _cantoPendiente!.Value;
        var cantador = JugadorQueCanto!.Value;

        if (respuesta == RespuestaCanto.Quiero)
        {
            var envidoJugador1 = CalcularEnvido(Jugador1Id);
            var envidoJugador2 = CalcularEnvido(Jugador2Id);
            var ganador = envidoJugador1 >= envidoJugador2 ? Jugador1Id : Jugador2Id;
            AsignarPuntos(ganador, PuntosPorQuiero(canto));
        }
        else
        {
            AsignarPuntos(cantador, PuntosPorNoQuiero(canto));
        }

        _cantoPendiente = null;

        // Si el envido empujo a algun jugador al PuntosObjetivo, AsignarPuntos ya cerro
        // la ronda (Fase.Finalizada) — no hay que seguir jugando cartas.
        if (Fase != FaseRonda.Finalizada)
        {
            Estado = EstadoRonda.JugandoCartas;
            TurnoActual = _turnoAntesDelCanto;
        }

        RegistrarActividad();
    }

    public void GritarTruco(ulong jugadorId, CantoTruco canto)
    {
        if (Fase == FaseRonda.Finalizada)
        {
            throw new InvalidOperationException("La ronda ya finalizo.");
        }

        // Escalada "tipo tenis": el rival que debe responder puede subir la apuesta
        // directo (ej. Retruco) sin decir "Quiero" antes. Eso implica aceptar el
        // canto pendiente y volver a cantar en el mismo gesto.
        var esEscaladaTenis = Estado == EstadoRonda.RespondiendoTruco;

        if (esEscaladaTenis)
        {
            // El gate correcto aca es "le toca responder", no TurnoCantoTruco: ese
            // todavia tiene el valor del ciclo anterior hasta que se aplique el
            // "Quiero" implicito mas abajo.
            if (jugadorId != TurnoActual)
            {
                throw new InvalidOperationException("No es el turno de este jugador.");
            }
        }
        else
        {
            if (Estado != EstadoRonda.JugandoCartas && Estado != EstadoRonda.EsperandoEnvido)
            {
                throw new InvalidOperationException("No se puede cantar truco en este momento.");
            }

            if (TurnoCantoTruco != null && TurnoCantoTruco != jugadorId)
            {
                throw new InvalidOperationException("Solo el jugador con derecho a subir la apuesta puede cantar.");
            }
        }

        var valorDeReferencia = esEscaladaTenis ? ValorDeCantoTruco(_cantoTrucoPendiente!.Value) : ValorTrucoActual;

        if (canto != SiguienteCantoTrucoEsperado(valorDeReferencia))
        {
            throw new InvalidOperationException("No se puede cantar eso ahora.");
        }

        if (esEscaladaTenis)
        {
            ResponderTruco(jugadorId, RespuestaCanto.Quiero);
        }

        _cantoTrucoPendiente = canto;
        JugadorQueGritoTruco = jugadorId;
        _estadoAntesDelTruco = Estado;
        _turnoAntesDelTruco = TurnoActual;
        Estado = EstadoRonda.RespondiendoTruco;
        TurnoActual = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;
        RegistrarActividad();
    }

    public void ResponderTruco(ulong jugadorId, RespuestaCanto respuesta)
    {
        if (jugadorId != TurnoActual)
        {
            throw new InvalidOperationException("No es el turno de este jugador.");
        }

        if (Estado != EstadoRonda.RespondiendoTruco)
        {
            throw new InvalidOperationException("No hay ningun truco pendiente para responder.");
        }

        var cantador = JugadorQueGritoTruco!.Value;

        if (respuesta == RespuestaCanto.Quiero)
        {
            ValorTrucoActual = ValorDeCantoTruco(_cantoTrucoPendiente!.Value);
            TurnoCantoTruco = jugadorId;
            _cantoTrucoPendiente = null;
            Estado = _estadoAntesDelTruco;
            TurnoActual = _turnoAntesDelTruco;
        }
        else
        {
            _cantoTrucoPendiente = null;
            TerminarManoActual(cantador, ValorTrucoActual);
        }

        RegistrarActividad();
    }

    public void IrseAlMazo(ulong jugadorId)
    {
        if (Fase == FaseRonda.Finalizada)
        {
            throw new InvalidOperationException("La ronda ya finalizo.");
        }

        var rival = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;
        TerminarManoActual(rival, ValorTrucoActual);
    }

    public int CalcularEnvido(ulong jugadorId)
    {
        var mano = jugadorId == Jugador1Id ? _manoOriginalJugador1 : _manoOriginalJugador2;
        return Gestor.MejorEnvido(mano);
    }

    public bool PuedeCantarEnvido(ulong jugadorId)
    {
        if (jugadorId != TurnoActual || EnvidoCantado)
        {
            return false;
        }

        var cartasJugadas = jugadorId == Jugador1Id ? _cartasJugadasJugador1.Count : _cartasJugadasJugador2.Count;
        return cartasJugadas == 0;
    }

    public bool EsPieza(Carta carta) => Gestor.EsPieza(carta);

    public int ValorPieza(Carta carta) => Gestor.EsPieza(carta) ? Gestor.ValorEnvido(carta) : 0;

    public bool TieneFlor(ulong jugadorId)
    {
        var mano = jugadorId == Jugador1Id ? _manoOriginalJugador1 : _manoOriginalJugador2;
        var piezas = mano.Where(EsPieza).ToList();

        if (piezas.Count >= 2)
        {
            return true;
        }

        if (piezas.Count == 1)
        {
            var restantes = mano.Where(c => !EsPieza(c)).ToList();
            return restantes[0].Palo == restantes[1].Palo;
        }

        return mano[0].Palo == mano[1].Palo && mano[1].Palo == mano[2].Palo;
    }

    public int CalcularPuntosFlor(ulong jugadorId)
    {
        var mano = jugadorId == Jugador1Id ? _manoOriginalJugador1 : _manoOriginalJugador2;
        var piezas = mano.Where(EsPieza).OrderByDescending(ValorPieza).ToList();

        if (piezas.Count == 0)
        {
            return 20 + mano.Sum(Gestor.ValorEnvido);
        }

        if (piezas.Count == 1)
        {
            var restantes = mano.Where(c => !EsPieza(c));
            return ValorPieza(piezas[0]) + restantes.Sum(Gestor.ValorEnvido);
        }

        var total = ValorPieza(piezas[0]);
        for (var i = 1; i < piezas.Count; i++)
        {
            total += ValorPieza(piezas[i]) % 10;
        }

        if (piezas.Count == 2)
        {
            var tercera = mano.First(c => !EsPieza(c));
            total += Gestor.ValorEnvido(tercera);
        }

        return total;
    }

    public void CantarFlor(ulong jugadorId)
    {
        if (jugadorId != TurnoActual)
        {
            throw new InvalidOperationException("Solo podés cantar Flor en tu turno.");
        }

        if (!PuedeCantarEnvido(jugadorId))
        {
            throw new InvalidOperationException("No se puede cantar Flor en este momento.");
        }

        if (!TieneFlor(jugadorId))
        {
            throw new InvalidOperationException("No tenés Flor, no seas fantasma.");
        }

        FlorCantada[jugadorId] = true;
        AsignarPuntos(jugadorId, 3);
        RegistrarActividad();
    }

    public void JugarCarta(ulong jugadorId, Carta carta)
    {
        if (Fase == FaseRonda.Finalizada)
        {
            throw new InvalidOperationException("La ronda ya finalizo.");
        }

        if (Estado == EstadoRonda.RespondiendoCanto || Estado == EstadoRonda.RespondiendoTruco)
        {
            throw new InvalidOperationException("Hay un canto pendiente de respuesta.");
        }

        if (jugadorId != TurnoActual)
        {
            throw new InvalidOperationException("No es el turno de este jugador.");
        }

        var mano = jugadorId == Jugador1Id ? _manoJugador1 : _manoJugador2;
        if (!mano.Remove(carta))
        {
            throw new ArgumentException("El jugador no tiene esa carta.", nameof(carta));
        }

        if (Estado == EstadoRonda.EsperandoEnvido)
        {
            Estado = EstadoRonda.JugandoCartas;
        }

        if (jugadorId == Jugador1Id)
        {
            _cartasJugadasJugador1.Add(carta);
            _cartaActualJugador1 = carta;
            TurnoActual = Jugador2Id;
        }
        else
        {
            _cartasJugadasJugador2.Add(carta);
            _cartaActualJugador2 = carta;
            TurnoActual = Jugador1Id;
        }

        if (_cartaActualJugador1 != null && _cartaActualJugador2 != null)
        {
            ResolverMano();
        }

        RegistrarActividad();
    }

    public void RegistrarActividad() => UltimaActividad = DateTime.UtcNow;

    private void ResolverMano()
    {
        var comparacion = Gestor.Comparar(_cartaActualJugador1!, _cartaActualJugador2!);

        ResultadoMano resultado;
        if (comparacion > 0)
        {
            resultado = ResultadoMano.Jugador1;
            TurnoActual = Jugador1Id;
        }
        else if (comparacion < 0)
        {
            resultado = ResultadoMano.Jugador2;
            TurnoActual = Jugador2Id;
        }
        else
        {
            resultado = ResultadoMano.Parda;
            TurnoActual = Jugador1Id;
        }

        // Guardar esto ANTES de limpiar/repartir de nuevo: la UI necesita poder mostrar
        // las ultimas dos cartas jugadas aunque IniciarSiguienteMano ya haya vaciado las
        // listas de cartas jugadas para el momento en que se arma el mensaje.
        UltimaCartaMesaJ1 = _cartaActualJugador1;
        UltimaCartaMesaJ2 = _cartaActualJugador2;
        GanadorUltimaMano = resultado == ResultadoMano.Parda ? null : TurnoActual;

        _resultadosManos.Add(resultado);
        _cartaActualJugador1 = null;
        _cartaActualJugador2 = null;

        if (EvaluarGanadorRonda())
        {
            // GanarRondaPorManos (via TerminarManoActual) ya dejo Fase en el estado correcto:
            // Finalizada si se alcanzo el PuntosObjetivo, o PrimeraMano si arranco una mano nueva.
            return;
        }

        Fase = Fase switch
        {
            FaseRonda.PrimeraMano => FaseRonda.SegundaMano,
            FaseRonda.SegundaMano => FaseRonda.TerceraMano,
            _ => FaseRonda.Finalizada,
        };
    }

    private bool EvaluarGanadorRonda()
    {
        var victorias1 = _resultadosManos.Count(r => r == ResultadoMano.Jugador1);
        var victorias2 = _resultadosManos.Count(r => r == ResultadoMano.Jugador2);

        if (victorias1 >= 2)
        {
            return GanarRondaPorManos(Jugador1Id);
        }

        if (victorias2 >= 2)
        {
            return GanarRondaPorManos(Jugador2Id);
        }

        if (_resultadosManos.Count == 1)
        {
            return false;
        }

        var primera = _resultadosManos[0];
        var segunda = _resultadosManos[1];

        if (_resultadosManos.Count == 2)
        {
            // Parda en la primera: gana la ronda quien gane la segunda. Parda en la segunda: gana quien gano la primera.
            if (primera != ResultadoMano.Parda && segunda == ResultadoMano.Parda)
            {
                return GanarRondaPorManos(GanadorDe(primera));
            }

            if (primera == ResultadoMano.Parda && segunda != ResultadoMano.Parda)
            {
                return GanarRondaPorManos(GanadorDe(segunda));
            }

            return false;
        }

        var tercera = _resultadosManos[2];

        if (primera == ResultadoMano.Parda && segunda == ResultadoMano.Parda && tercera == ResultadoMano.Parda)
        {
            return GanarRondaPorManos(Jugador1Id);
        }

        if (tercera != ResultadoMano.Parda)
        {
            return GanarRondaPorManos(GanadorDe(tercera));
        }

        var primeraNoParda = _resultadosManos.First(r => r != ResultadoMano.Parda);
        return GanarRondaPorManos(GanadorDe(primeraNoParda));
    }

    private bool GanarRondaPorManos(ulong ganador)
    {
        TerminarManoActual(ganador, ValorTrucoActual);
        return true;
    }

    private ulong GanadorDe(ResultadoMano resultado) => resultado == ResultadoMano.Jugador1 ? Jugador1Id : Jugador2Id;

    // Punto de cierre comun para toda mano que termina (2/3 bazas, parda, truco rechazado o
    // irse al mazo): suma los puntos y, si nadie llego al PuntosObjetivo, arranca la siguiente
    // mano en vez de cortar la partida.
    private void TerminarManoActual(ulong ganadorDeLaMano, int puntos)
    {
        AsignarPuntos(ganadorDeLaMano, puntos);

        if (Fase != FaseRonda.Finalizada)
        {
            IniciarSiguienteMano();
        }
    }

    private void IniciarSiguienteMano()
    {
        JugadorManoId = JugadorManoId == Jugador1Id ? Jugador2Id : Jugador1Id;

        _cartasJugadasJugador1.Clear();
        _cartasJugadasJugador2.Clear();
        _resultadosManos.Clear();
        _cartaActualJugador1 = null;
        _cartaActualJugador2 = null;

        var mazo = new Mazo();
        mazo.Mezclar();
        var reparto = mazo.Repartir();

        _manoJugador1.Clear();
        _manoJugador1.AddRange(reparto.ManoJugador1);
        _manoJugador2.Clear();
        _manoJugador2.AddRange(reparto.ManoJugador2);
        _manoOriginalJugador1 = _manoJugador1.ToArray();
        _manoOriginalJugador2 = _manoJugador2.ToArray();
        Muestra = reparto.Muestra;
        Gestor = new GestorDeJerarquia(Muestra);

        _cantoPendiente = null;
        JugadorQueCanto = null;
        EnvidoCantado = false;
        _cantoTrucoPendiente = null;
        JugadorQueGritoTruco = null;
        ValorTrucoActual = 1;
        TurnoCantoTruco = null;
        FlorCantada.Clear();

        Fase = FaseRonda.PrimeraMano;
        Estado = EstadoRonda.EsperandoEnvido;
        TurnoActual = JugadorManoId;
        RegistrarActividad();
    }

    private void AsignarPuntos(ulong jugadorId, int puntos)
    {
        if (jugadorId == Jugador1Id)
        {
            PuntosJugador1 += puntos;
        }
        else
        {
            PuntosJugador2 += puntos;
        }

        // Si con estos puntos alguno llega al objetivo, la partida termina ahi mismo,
        // incluso si quedaban cartas por jugar (asi funciona el envido en el truco real).
        if (PuntosJugador1 >= PuntosObjetivo || PuntosJugador2 >= PuntosObjetivo)
        {
            GanadorRonda = PuntosJugador1 >= PuntosObjetivo ? Jugador1Id : Jugador2Id;
            Fase = FaseRonda.Finalizada;
        }
    }

    private int PuntosPorQuiero(Canto canto) => canto switch
    {
        Canto.Envido => 2,
        Canto.RealEnvido => 3,
        // Falta Envido: lo que le falta al jugador que va ganando para llegar al objetivo.
        Canto.FaltaEnvido => PuntosObjetivo - Math.Max(PuntosJugador1, PuntosJugador2),
        _ => throw new ArgumentOutOfRangeException(nameof(canto)),
    };

    private static int PuntosPorNoQuiero(Canto canto) => 1;

    private static CantoTruco SiguienteCantoTrucoEsperado(int valorTrucoActual) => valorTrucoActual switch
    {
        1 => CantoTruco.Truco,
        2 => CantoTruco.Retruco,
        3 => CantoTruco.ValeCuatro,
        _ => throw new InvalidOperationException("Ya se canto ValeCuatro, no se puede subir mas."),
    };

    private static int ValorDeCantoTruco(CantoTruco canto) => canto switch
    {
        CantoTruco.Truco => 2,
        CantoTruco.Retruco => 3,
        CantoTruco.ValeCuatro => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(canto)),
    };
}
