# Diseño: Mazo y Ronda — Truco Uruguayo

## Contexto

Sobre `TrucoUruguayo.Core` ya existente (`Carta`, `Palo`, `Pieza`, `GestorDeJerarquia`,
namespaces `Modelo` y `Jerarquia`), se agrega el mazo de cartas y la orquestación
básica de una ronda de juego: quién tiene qué mano, cuál es la muestra, y en qué
fase está la ronda. No incluye todavía la lógica de jugar cartas ni cantar
Envido/Truco — eso es para un pedido futuro.

## Alcance

- `Mazo`: las 40 cartas de la baraja española (sin 8 ni 9), mezclar, repartir.
- `Reparto`: resultado de un reparto (muestra + dos manos de 3).
- `Ronda`: agrupa dos jugadores, arma su propio `Mazo` y `GestorDeJerarquia`,
  expone el estado de la ronda y de quién es el turno.
- Tests xUnit que verifiquen el reparto: nadie recibe la muestra, ambos
  jugadores reciben exactamente 3 cartas, sin superposición entre manos.

**Fuera de alcance** (pedidos futuros): jugar una carta, cantar Envido/Truco,
lógica real de turnos (quién es mano, rotación entre rondas), integración con
Discord, persistencia en Postgres.

## Diseño

### Ubicación

Nuevo namespace `TrucoUruguayo.Core.Juego`, en paralelo a `Modelo` y
`Jerarquia`: `src/TrucoUruguayo.Core/Juego/Mazo.cs` y
`src/TrucoUruguayo.Core/Juego/Ronda.cs`.

### `Mazo`

```csharp
public class Mazo
{
    public Mazo();             // arma las 40 cartas (4 palos x {1-7,10,11,12}), sin barajar
    public void Mezclar();     // REGENERA las 40 cartas completas y las baraja (Fisher-Yates)
    public Reparto Repartir(); // saca 7 cartas del estado actual: Muestra + 3 + 3
}
```

Punto clave (aclarado por el usuario): `Mezclar()` no baraja "lo que quede" del
mazo — cada llamada reconstruye el mazo completo de 40 cartas desde cero y
recién ahí lo baraja. Esto garantiza que cada reparto sea siempre sobre las 40
cartas, siempre y cuando se llame `Mezclar()` antes de cada `Repartir()` (que
es exactamente lo que hace `Ronda` en su constructor). `Repartir()` en sí
mismo no vuelve a barajar ni valida que se haya llamado `Mezclar()` antes —
simplemente reparte del estado actual del mazo.

`Repartir()` saca la Muestra primero (posición 0 del mazo ya barajado), y
después 3 cartas para cada jugador — así la Muestra queda aislada del mazo
antes de repartir las manos, nunca puede terminar en una mano.

### `Reparto`

Clase (record) en vez de tupla, por legibilidad:

```csharp
public sealed record Reparto(Carta Muestra, List<Carta> ManoJugador1, List<Carta> ManoJugador2);
```

### `Ronda`

```csharp
public enum EstadoRonda { EsperandoEnvido, JugandoCartas, Finalizada }

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
        // crea un Mazo, lo mezcla, reparte, arma el GestorDeJerarquia con la Muestra,
        // Estado = EsperandoEnvido, TurnoActual = jugador1Id
    }
}
```

Decisiones ya confirmadas con el usuario:
- IDs de jugador: `ulong` (pensando en IDs de Discord).
- Nombre de la clase: `Ronda`, no `Mano` (evita la colisión con "mano" como
  la mano de 3 cartas y con "ser mano"/jugador que arranca).
- `Estado` arranca en `EsperandoEnvido`.
- `TurnoActual` arranca en `Jugador1Id` — supuesto simple porque todavía no
  hay lógica real de turnos; se puede revisar cuando se implemente jugar
  cartas.
- El `Mazo` interno de la ronda no se expone como propiedad pública.

## Testing

Unit tests (xUnit) cubriendo:

- `Mazo`: al construir, exactamente 40 cartas únicas (4 palos x 10 números).
  `Repartir()` da Muestra + dos manos de 3, sin superposición entre manos ni
  con la Muestra — corrido en bucle (~50 iteraciones, cada una con su propio
  `Mazo` + `Mezclar()`) para no depender de un orden de barajado particular.
- `Ronda`: al construirse, ningún jugador tiene la Muestra en su mano, ambos
  tienen exactamente 3 cartas sin superposición entre sí, `Estado ==
  EsperandoEnvido`, `TurnoActual == Jugador1Id`, y `Gestor` quedó armado con
  la Muestra correcta (verificable comparando `Gestor.ObtenerPieza`/
  `ValorTruco` de la Muestra con lo esperado dado su propio Numero/Palo).
