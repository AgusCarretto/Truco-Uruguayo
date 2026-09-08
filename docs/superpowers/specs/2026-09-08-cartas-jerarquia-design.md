# Diseño: Clases base de Cartas y Jerarquía — Truco Uruguayo

## Contexto

Bot de Truco Uruguayo en C#. La partida vive en memoria, sin persistencia por
ahora. Se necesita el modelo de datos de las cartas y un motor de jerarquía
(`GestorDeJerarquia`) que, dada la carta de "muestra", calcule los valores de
Truco y Envido de cualquier otra carta, incluyendo el sistema de "piezas" de
la variante uruguaya.

## Alcance

- Enums `Palo` y `Pieza` (dados por el usuario).
- Clase `Carta` (Numero 1-12 salteando 8 y 9, Palo).
- `GestorDeJerarquia`: identificación de piezas, promoción del 12, valores de
  Truco y Envido por carta, comparador de Truco, mejor Envido de una mano de
  3 cartas.

**Fuera de alcance** (para pedidos futuros): Flor, persistencia en Postgres,
mazo/deck y repartido, turnos/"mano", resolución de rondas o partidas.

## Reglas confirmadas con el usuario

### Piezas

Una carta es "pieza" si su `Palo` coincide con el `Palo` de la muestra Y su
`Numero` ∈ {2, 4, 5, 11, 10} (mapeado a `Pieza.Dos` / `Cuatro` / `Cinco` /
`Caballo` / `Sota` respectivamente).

Si la muestra en sí es una combinación pieza (ej. sale "4 de Oro" como
muestra), el 12 de ese mismo palo toma el lugar de esa pieza: hereda tanto
su valor de Truco como su valor de Envido.

### Valor de Envido (por carta)

| Carta                        | Envido |
|-------------------------------|--------|
| Pieza Dos                     | 30     |
| Pieza Cuatro                  | 29     |
| Pieza Cinco                   | 28     |
| Pieza Caballo                 | 27     |
| Pieza Sota                    | 27     |
| 10 / 11 / 12 "negras" (no pieza, distinto palo que la muestra) | 0 |
| Cartas comunes 1 a 7          | su propio número |

### Mejor Envido de mano (3 cartas)

- Si la mano contiene al menos una pieza: resultado = valor de Envido de la
  pieza más alta que tenga + el mayor valor de Envido entre las otras dos
  cartas. **No** se aplica el +20 clásico de mismo palo en este caso.
- Si no hay pieza en la mano: regla clásica → mejor par de mismo palo entre
  las 3 cartas + 20; si no hay par, la carta suelta de mayor valor.

### Valor de Truco (por carta)

Las 5 piezas están siempre por encima de cualquier carta común, en el orden:

```
Dos > Cuatro > Cinco > Caballo > Sota
```

Debajo, jerarquía clásica argentina/uruguaya con mazo español:

```
1 Espada > 1 Basto > 7 Espada > 7 Oro > 3 > 2 > (1 Copa = 1 Oro)
  > 12 > 11 > 10 > (7 Copa = 7 Basto) > 6 > 5 > 4
```

Los empates (mismo valor entero) se resuelven fuera de `GestorDeJerarquia`
(ej. con la regla de "gana mano" al momento de comparar en la partida).
`Comparar` devuelve 0 en ese caso.

## API

```csharp
public enum Palo { Espada, Basto, Oro, Copa }
public enum Pieza { Dos, Cuatro, Cinco, Caballo, Sota }

public class Carta
{
    public int Numero { get; }
    public Palo Palo { get; }

    public Carta(int numero, Palo palo);
    // Valida Numero ∈ {1..7, 10, 11, 12}; arroja ArgumentOutOfRangeException si no.
    // Implementa Equals/GetHashCode/ToString por valor.
}

public class GestorDeJerarquia
{
    public GestorDeJerarquia(Carta muestra);

    public bool EsPieza(Carta carta);
    public Pieza? ObtenerPieza(Carta carta); // null si no es pieza

    public int ValorTruco(Carta carta);   // más alto = mejor
    public int ValorEnvido(Carta carta);  // valor de UNA carta

    public int Comparar(Carta a, Carta b); // >0 gana a, <0 gana b, 0 empate
    public int MejorEnvido(Carta[] mano);  // longitud exactamente 3, arroja si no
}
```

## Notas de implementación

- `ValorTruco` es puramente ordinal: el entero en sí no representa puntos ni
  tiene significado fuera de esta clase, solo sirve para poder comparar dos
  cartas y saber cuál gana la mano (a diferencia del Envido, donde el número
  sí es un valor real del juego). Internamente las piezas ocupan los enteros
  más altos (19 a 15, de Dos a Sota) y la jerarquía clásica baja de 14 a 1,
  con empates compartiendo el mismo entero (ej. 1 Copa y 1 Oro ambos valen lo
  mismo) — pero esa numeración exacta es un detalle de implementación, no
  una regla del juego.
- `GestorDeJerarquia` no modela el mazo, el descarte de la muestra, los
  turnos entre jugadores ni la ventana en la que se puede cantar Envido
  (solo en la primera ronda de cada mano). Esas reglas de flujo de juego
  quedan para una clase futura (tipo `Ronda`/`Mano`) que sea quien orqueste
  la partida turno a turno; `GestorDeJerarquia` solo responde "¿cuánto vale
  esta carta?" y "¿quién gana entre estas dos?" cuando esa otra clase se lo
  pregunte.

## Testing

Unit tests (xUnit) cubriendo:

- Identificación de pieza por carta (`EsPieza` / `ObtenerPieza`).
- Promoción del 12 cuando la muestra es una pieza (Truco y Envido heredados).
- Valores de Envido por carta, incluyendo negras y piezas.
- `MejorEnvido` con y sin pieza en la mano.
- Orden completo de Truco, incluyendo los empates clásicos (1 Copa/1 Oro,
  7 Copa/7 Basto).
