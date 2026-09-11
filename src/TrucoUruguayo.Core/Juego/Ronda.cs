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
    public Carta Muestra { get; }
    public IReadOnlyList<Carta> ManoJugador1 => _manoJugador1;
    public IReadOnlyList<Carta> ManoJugador2 => _manoJugador2;
    public IReadOnlyList<Carta> CartasJugadasJugador1 => _cartasJugadasJugador1;
    public IReadOnlyList<Carta> CartasJugadasJugador2 => _cartasJugadasJugador2;
    public GestorDeJerarquia Gestor { get; }
    public EstadoRonda Estado { get; private set; }
    public FaseRonda Fase { get; private set; }
    public ulong TurnoActual { get; private set; }
    public ulong? GanadorRonda { get; private set; }
    public int PuntosJugador1 { get; private set; }
    public int PuntosJugador2 { get; private set; }
    public ulong? JugadorQueCanto { get; private set; }
    public int PuntosFaltaEnvido { get; set; } = 30;
    public int ValorTrucoActual { get; private set; } = 1;
    public ulong? TurnoCantoTruco { get; private set; }
    public ulong? JugadorQueGritoTruco { get; private set; }

    public Ronda(ulong jugador1Id, ulong jugador2Id)
    {
        var mazo = new Mazo();
        mazo.Mezclar();
        var reparto = mazo.Repartir();

        Jugador1Id = jugador1Id;
        Jugador2Id = jugador2Id;
        Muestra = reparto.Muestra;
        _manoJugador1 = reparto.ManoJugador1;
        _manoJugador2 = reparto.ManoJugador2;
        Gestor = new GestorDeJerarquia(Muestra);

        Estado = EstadoRonda.EsperandoEnvido;
        Fase = FaseRonda.PrimeraMano;
        TurnoActual = jugador1Id;
    }

    internal Ronda(ulong jugador1Id, ulong jugador2Id, Carta muestra, List<Carta> manoJugador1, List<Carta> manoJugador2)
    {
        Jugador1Id = jugador1Id;
        Jugador2Id = jugador2Id;
        Muestra = muestra;
        _manoJugador1 = manoJugador1;
        _manoJugador2 = manoJugador2;
        Gestor = new GestorDeJerarquia(Muestra);

        Estado = EstadoRonda.EsperandoEnvido;
        Fase = FaseRonda.PrimeraMano;
        TurnoActual = jugador1Id;
    }

    public void CantarEnvido(ulong jugadorId, Canto canto)
    {
        if (jugadorId != TurnoActual)
        {
            throw new InvalidOperationException("No es el turno de este jugador.");
        }

        if (Estado != EstadoRonda.EsperandoEnvido)
        {
            throw new InvalidOperationException("No se puede cantar un envido en este momento.");
        }

        if (Fase != FaseRonda.PrimeraMano)
        {
            throw new InvalidOperationException("Ya no se puede cantar envido.");
        }

        _cantoPendiente = canto;
        JugadorQueCanto = jugadorId;
        _turnoAntesDelCanto = TurnoActual;
        Estado = EstadoRonda.RespondiendoCanto;
        TurnoActual = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;
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
            var envidoJugador1 = Gestor.MejorEnvido(_manoJugador1.ToArray());
            var envidoJugador2 = Gestor.MejorEnvido(_manoJugador2.ToArray());
            var ganador = envidoJugador1 >= envidoJugador2 ? Jugador1Id : Jugador2Id;
            AsignarPuntos(ganador, PuntosPorQuiero(canto));
        }
        else
        {
            AsignarPuntos(cantador, PuntosPorNoQuiero(canto));
        }

        _cantoPendiente = null;
        Estado = EstadoRonda.JugandoCartas;
        TurnoActual = _turnoAntesDelCanto;
    }

    public void GritarTruco(ulong jugadorId, CantoTruco canto)
    {
        if (Fase == FaseRonda.Finalizada)
        {
            throw new InvalidOperationException("La ronda ya finalizo.");
        }

        if (Estado != EstadoRonda.JugandoCartas && Estado != EstadoRonda.EsperandoEnvido)
        {
            throw new InvalidOperationException("No se puede cantar truco en este momento.");
        }

        if (TurnoCantoTruco != null && TurnoCantoTruco != jugadorId)
        {
            throw new InvalidOperationException("Solo el jugador con derecho a subir la apuesta puede cantar.");
        }

        if (canto != SiguienteCantoTrucoEsperado(ValorTrucoActual))
        {
            throw new InvalidOperationException("No se puede cantar eso ahora.");
        }

        _cantoTrucoPendiente = canto;
        JugadorQueGritoTruco = jugadorId;
        _estadoAntesDelTruco = Estado;
        _turnoAntesDelTruco = TurnoActual;
        Estado = EstadoRonda.RespondiendoTruco;
        TurnoActual = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;
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
            AsignarPuntos(cantador, ValorTrucoActual);
            GanadorRonda = cantador;
            _cantoTrucoPendiente = null;
            Fase = FaseRonda.Finalizada;
        }
    }

    public void IrseAlMazo(ulong jugadorId)
    {
        if (Fase == FaseRonda.Finalizada)
        {
            throw new InvalidOperationException("La ronda ya finalizo.");
        }

        var rival = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;
        AsignarPuntos(rival, ValorTrucoActual);
        GanadorRonda = rival;
        Fase = FaseRonda.Finalizada;
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
    }

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

        _resultadosManos.Add(resultado);
        _cartaActualJugador1 = null;
        _cartaActualJugador2 = null;

        if (EvaluarGanadorRonda())
        {
            Fase = FaseRonda.Finalizada;
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
            GanadorRonda = Jugador1Id;
            return true;
        }

        if (victorias2 >= 2)
        {
            GanadorRonda = Jugador2Id;
            return true;
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
                GanadorRonda = GanadorDe(primera);
                return true;
            }

            if (primera == ResultadoMano.Parda && segunda != ResultadoMano.Parda)
            {
                GanadorRonda = GanadorDe(segunda);
                return true;
            }

            return false;
        }

        var tercera = _resultadosManos[2];

        if (primera == ResultadoMano.Parda && segunda == ResultadoMano.Parda && tercera == ResultadoMano.Parda)
        {
            GanadorRonda = Jugador1Id;
            return true;
        }

        if (tercera != ResultadoMano.Parda)
        {
            GanadorRonda = GanadorDe(tercera);
            return true;
        }

        var primeraNoParda = _resultadosManos.First(r => r != ResultadoMano.Parda);
        GanadorRonda = GanadorDe(primeraNoParda);
        return true;
    }

    private ulong GanadorDe(ResultadoMano resultado) => resultado == ResultadoMano.Jugador1 ? Jugador1Id : Jugador2Id;

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
    }

    private int PuntosPorQuiero(Canto canto) => canto switch
    {
        Canto.Envido => 2,
        Canto.RealEnvido => 3,
        Canto.FaltaEnvido => PuntosFaltaEnvido,
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
