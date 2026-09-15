# Sistema de Flor

## Contexto

Se agrega la Flor al motor de Truco Uruguayo (`Ronda.cs`), con anuncio y validaciones en `TrucoModule.cs`. La Flor se calcula sobre "piezas" (cartas del palo de la muestra con números 2, 4, 5, 11 o 10, incluyendo el caso de "pieza promovida" cuando la muestra es justo una de esas piezas).

## Correcciones sobre el pedido original

- `GestorDeJerarquia` (expuesto como `Ronda.Gestor`) ya tiene `EsPieza(Carta)` y `ValorEnvido(Carta)`, que devuelven exactamente los valores pedidos (30/29/28/27/27 para 2/4/5/11/10 del palo de la muestra) y además manejan correctamente la "pieza promovida" (cuando la muestra es ella misma un 2/4/5/11/10, el 12 de ese palo pasa a ocupar esa pieza). `Ronda.EsPieza`/`Ronda.ValorPieza` van a **delegar** en `Gestor` en vez de reimplementar la comparación de número exacto, para no duplicar la lógica y no perder ese caso.
- Toda la validación de reglas del juego (turno, si se puede cantar algo) vive hoy **dentro de `Ronda.cs`** — `CantarEnvido`, `GritarTruco`, `JugarCarta`, `IrseAlMazo` tiran `InvalidOperationException` con el mensaje de error, y `TrucoModule` solo hace `try { ronda.Metodo(...); } catch (InvalidOperationException ex) { RespondAsync($"⚠️ {ex.Message}", ephemeral: true); }`. La Flor sigue ese mismo patrón: `Ronda.CantarFlor` valida y muta estado; el chequeo "tiene flor sin cantar" vive dentro de `CantarEnvido` y `JugarCarta` (no como `if` sueltos en `TrucoModule`). Esto además hace que la regla quede testeada en `Core.Tests` junto con el resto de las reglas de la ronda, no solo en el bot.
- `JugarCarta` (el `[ComponentInteraction]` en `TrucoModule`) hoy llama a `ronda.JugarCarta(...)` **sin** try/catch (a diferencia de todos los demás handlers de canto). Se le agrega, para que el nuevo chequeo de flor (y cualquier excepción futura de `Ronda.JugarCarta`) se muestre como mensaje en vez de rompear la interacción.

## `Ronda.cs`

### `EsPieza` / `ValorPieza`

```csharp
public bool EsPieza(Carta carta) => Gestor.EsPieza(carta);

public int ValorPieza(Carta carta) => Gestor.EsPieza(carta) ? Gestor.ValorEnvido(carta) : 0;
```

### `TieneFlor(ulong jugadorId)`

Usa `_manoOriginalJugador1`/`_manoOriginalJugador2` (la mano original de 3 cartas de esta ronda — igual razón que el envido: `TieneFlor` se puede consultar aunque el jugador ya haya jugado una carta de la mano actual).

- 2 o 3 piezas → `true`.
- 1 pieza → `true` si las otras 2 cartas comparten palo entre sí.
- 0 piezas → `true` si las 3 cartas comparten el mismo palo.
- Cualquier otro caso → `false`.

### `CalcularPuntosFlor(ulong jugadorId)`

Sobre la misma mano original de 3 cartas, con las piezas ordenadas de mayor a menor `ValorPieza`:

- 0 piezas: `20 + Σ Gestor.ValorEnvido(carta)` de las 3 cartas (una carta "blanca" — 10/11/12 que no es pieza — vale 0 vía `Gestor.ValorEnvido`).
- 1 pieza: `ValorPieza(pieza) + Σ Gestor.ValorEnvido(carta)` de las otras 2 cartas.
- 2 piezas: `ValorPieza(pieza_más_alta) + (ValorPieza(pieza_2) % 10) + Gestor.ValorEnvido(carta_no_pieza)`. Ejemplo verificado: 2 y 4 → `30 + 9` (el 9 es el dígito de unidades de 29) `+ valor de la tercera carta`.
- 3 piezas: `ValorPieza(pieza_más_alta) + (ValorPieza(pieza_2) % 10) + (ValorPieza(pieza_3) % 10)`. Ejemplo verificado con el dado por el usuario: 2, 4 y 5 → `30 + 9 + 8 = 47`.

(Solo la pieza más alta aporta su valor completo — que ya incluye el "+20" de la flor — porque las piezas restantes solo suman su dígito de unidades, evitando contar el bono de flor más de una vez.)

### `FlorCantada`

```csharp
public Dictionary<ulong, bool> FlorCantada { get; private set; }
```

Se inicializa `= new()` en ambos constructores (el público y el `internal` usado por los tests) y se limpia (`FlorCantada.Clear()`) en `IniciarSiguienteMano()`, igual que el resto del estado de cantos de la mano.

### `CantarFlor(ulong jugadorId)`

```csharp
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
```

No cambia `Estado` ni `TurnoActual` (lógica de "flor directa" simplificada: no hay quiero/no quiero, son 3 puntos automáticos). Si `AsignarPuntos` deja `Fase` en `Finalizada` (llegó al objetivo), `TrucoModule` lo detecta después de la llamada, igual que hace `ResponderEnvidoAsync` hoy.

### Chequeo "flor pendiente" en `CantarEnvido` y `JugarCarta`

Inmediatamente **después** del chequeo de turno existente en cada método (no antes: si no es el turno del jugador, el error tiene que seguir siendo "no es tu turno", no el de la flor) se agrega:

```csharp
if (TieneFlor(jugadorId) && !FlorCantada.GetValueOrDefault(jugadorId))
{
    throw new InvalidOperationException("¡Tenés Flor! Debés cantarla antes del envido.");
}
```

con el texto `"¡Tenés Flor! Debés cantarla antes de jugar una carta."` en `JugarCarta`. En `CantarEnvido` esto queda entre el chequeo de turno (`jugadorId != TurnoActual`) y el de canto pendiente (`Estado == RespondiendoCanto || RespondiendoTruco`). En `JugarCarta` el chequeo de turno es el tercero del método (después de `Fase == Finalizada` y `Estado == RespondiendoCanto || RespondiendoTruco`), así que el de flor va justo después de ese, antes de tocar la mano.

## `TrucoModule.cs`

### `ConstruirBotonesDeAccion`

El `SelectMenuBuilder` de envido (que ya solo se muestra `if (ronda.PuedeCantarEnvido(ronda.TurnoActual))`, la misma ventana de tiempo en la que se puede cantar Flor) suma una opción:

```csharp
.AddOption("🌸 Flor", "flor")
```

### `SeleccionarEnvido`

El `switch` sobre `opciones[0]` gana una rama para `"flor"` que no pasa por el flujo de `Canto`/nombre-para-mostrar existente: se maneja aparte, antes del switch actual.

```csharp
if (opciones[0] == "flor")
{
    try
    {
        ronda.CantarFlor(Context.User.Id);
    }
    catch (InvalidOperationException ex)
    {
        await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        return;
    }

    var puntosFlor = ronda.CalcularPuntosFlor(Context.User.Id);

    await Context.Channel.SendMessageAsync(
        $"{GenerarTextoMarcador(ronda)}🌸 ¡<@{Context.User.Id}> cantó FLOR ({puntosFlor} puntos)!");

    if (ronda.Fase == FaseRonda.Finalizada)
    {
        await FinalizarYAnunciarRonda(ronda);
    }
    else
    {
        await Context.Channel.SendMessageAsync(
            $"👉 Turno de <@{ronda.TurnoActual}>.",
            components: ConstruirBotonesDeAccion(ronda));
    }

    await DeferAsync();
    return;
}
```

El resto del método (envido/real_envido/falta_envido) sigue igual — el chequeo de "flor pendiente" ya lo cubre `ronda.CantarEnvido` internamente, así que el `try/catch` que ya existe ahí lo muestra sin cambios adicionales.

### `JugarCarta`

Se envuelve la llamada existente:

```csharp
ronda.JugarCarta(jugadorQueJuega, carta);
```

en:

```csharp
try
{
    ronda.JugarCarta(jugadorQueJuega, carta);
}
catch (InvalidOperationException ex)
{
    await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
    return;
}
```

(mismo patrón que el resto de los handlers; hoy es el único que llama a un método de `Ronda` que puede tirar sin capturarlo).

## Testing

`tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs` (unitario, sin DB — usa el helper existente `NuevaRondaConMazoFijo`, con `Muestra = new(3, Palo.Oro)`, así que las piezas son 2/4/5/11/10 de Oro):

- `EsPieza`/`ValorPieza`: una carta pieza (ej. `new Carta(2, Palo.Oro)`) y una que no lo es.
- `TieneFlor`: casos de 3 piezas, 2 piezas, 1 pieza + resto mismo palo, 1 pieza + resto distinto palo (false), 0 piezas + 3 mismo palo, 0 piezas + palos mixtos (false).
- `CalcularPuntosFlor`: los 4 casos (0, 1, 2 y 3 piezas), incluyendo el ejemplo del usuario (2+4+5 de Oro → 47).
- `CantarFlor`: caso feliz (marca `FlorCantada`, suma 3 puntos), sin flor (excepción con el mensaje esperado), fuera de turno (excepción).
- `CantarEnvido` y `JugarCarta`: con flor sin cantar, tiran `InvalidOperationException` con el mensaje correspondiente; después de `CantarFlor`, ya no bloquean.

No se agregan tests automatizados para los cambios en `TrucoModule.cs` (comandos/componentes de Discord) — mismo criterio que el resto de los módulos de interacción en este repo.
