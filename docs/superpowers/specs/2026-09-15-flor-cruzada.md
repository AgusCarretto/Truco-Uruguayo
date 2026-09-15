# Cruce de Flores (Flor contra Flor) y anulación de Envido

## Contexto

Hoy `Ronda.CantarFlor` es "directa": siempre suma 3 puntos fijos al que canta, sin importar si el rival también tiene Flor, y no anula el Envido. Este spec agrega el cruce real: si el rival también tiene Flor, se abre una escalada (`Flor` → opcional `Con Flor Envido`/`Contra Flor al Resto` → `Quiero`/`No Quiero`), y si el rival no tiene, el Envido queda anulado para esa mano.

## Completions necesarias sobre el pedido original

El pedido no menciona estos puntos explícitamente, pero son necesarios para que el comportamiento pedido ("el Envido se anula", "de quién es el turno en todo momento") funcione de verdad:

- **`EnvidoCantado = true`** se marca en los 3 puntos donde la Flor queda resuelta (sin cruce en `CantarFlor`, `"la_mia_es_flor"` en `ResponderFlor`, y ambas ramas de `ResponderContraFlor`). `PuedeCantarEnvido` — lo que de verdad habilita el select menu de Envido — depende de esta propiedad, no de `Estado`.
- **`ConstruirBotonesDeAccion`** hoy solo entra en su bloque de botones `if (Estado == EsperandoEnvido || Estado == JugandoCartas)`. Los dos estados nuevos quedarían sin ningún botón si no se les agrega su propia rama.
- **`PuntosFlorActuales`** (y el campo que guarda el valor previo al último aumento) se resetean en `IniciarSiguienteMano`, igual que `ValorTrucoActual`/`EnvidoCantado`/`FlorCantada` — el inicializador `= 0` de la propiedad solo corre una vez, al construir la `Ronda`.
- **Comunicar ganador y puntos a la UI**: `ResponderFlor`/`ResponderContraFlor` son `void` (tal como se pidió). En vez de agregar una propiedad nueva a `Ronda`, `TrucoModule` compara `PuntosJugador1`/`PuntosJugador2` antes y después de la llamada — mismo patrón ya usado para el Envido.
- **`FlorCantada[Jugador1Id]` y `FlorCantada[Jugador2Id]`** se marcan ambas en `true` al resolver un cruce (`"la_mia_es_flor"` y `ResponderContraFlor`), porque en el cruce **ambos** jugadores tienen Flor y ambos deben quedar habilitados para cantar Envido/jugar carta después.
- **`ResponderContraFlor`, rama "No Quiero"**: se guarda el valor de `PuntosFlorActuales` justo antes del último aumento en un campo privado (`_puntosFlorAntesDelUltimoAumento`), en vez de un número fijo (`- 2` o `6`) — así funciona para cualquiera de las dos rutas de escalada sin casos especiales.
- **Validación de turno y estado en `ResponderFlor`/`ResponderContraFlor`**: se agregan los mismos chequeos que ya tienen `ResponderEnvido`/`ResponderTruco` (turno correcto, estado correcto pendiente) — el pedido no los detalla pero es el patrón establecido en todo el resto de `Ronda.cs`.

No hacen falta cambios en `CantarEnvido`, `JugarCarta` ni `GritarTruco` más allá de lo que ya existe: sus chequeos actuales (turno, y el bloqueo "tenés Flor sin cantar" agregado en la feature anterior) ya impiden que cualquiera de los dos jugadores interrumpa un cruce de Flor pendiente — el jugador que debe responder el cruce todavía tiene su propia Flor sin resolver (`FlorCantada` en `false`), así que el bloqueo existente ya lo frena.

## `EstadoRonda.cs`

```csharp
public enum EstadoRonda
{
    EsperandoEnvido,
    RespondiendoCanto,
    JugandoCartas,
    RespondiendoTruco,
    RespondiendoFlor,
    RespondiendoContraFlor,
}
```

## `Ronda.cs`

Nueva propiedad y campo privado:

```csharp
public int PuntosFlorActuales { get; private set; }
private int _puntosFlorAntesDelUltimoAumento;
```

### `CantarFlor` (reemplaza la versión actual)

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
    var rivalId = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;

    if (!TieneFlor(rivalId))
    {
        AsignarPuntos(jugadorId, 3);
        EnvidoCantado = true;

        if (Fase != FaseRonda.Finalizada)
        {
            Estado = EstadoRonda.JugandoCartas;
            TurnoActual = JugadorManoId;
        }
    }
    else
    {
        Estado = EstadoRonda.RespondiendoFlor;
        PuntosFlorActuales = 6;
        TurnoActual = rivalId;
    }

    RegistrarActividad();
}
```

### `ResponderFlor` (nuevo)

```csharp
public void ResponderFlor(ulong jugadorId, string accion)
{
    if (jugadorId != TurnoActual)
    {
        throw new InvalidOperationException("No es el turno de este jugador.");
    }

    if (Estado != EstadoRonda.RespondiendoFlor)
    {
        throw new InvalidOperationException("No hay ninguna Flor pendiente de responder.");
    }

    var rival = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;

    switch (accion)
    {
        case "la_mia_es_flor":
            var ganador = CalcularPuntosFlor(Jugador1Id) >= CalcularPuntosFlor(Jugador2Id) ? Jugador1Id : Jugador2Id;
            AsignarPuntos(ganador, PuntosFlorActuales);
            FlorCantada[Jugador1Id] = true;
            FlorCantada[Jugador2Id] = true;
            EnvidoCantado = true;

            if (Fase != FaseRonda.Finalizada)
            {
                Estado = EstadoRonda.JugandoCartas;
                TurnoActual = JugadorManoId;
            }
            break;

        case "con_flor_envido":
            _puntosFlorAntesDelUltimoAumento = PuntosFlorActuales;
            PuntosFlorActuales += 2;
            Estado = EstadoRonda.RespondiendoContraFlor;
            TurnoActual = rival;
            break;

        case "contra_flor_al_resto":
            _puntosFlorAntesDelUltimoAumento = PuntosFlorActuales;
            PuntosFlorActuales = PuntosObjetivo - Math.Max(PuntosJugador1, PuntosJugador2);
            Estado = EstadoRonda.RespondiendoContraFlor;
            TurnoActual = rival;
            break;

        default:
            throw new ArgumentOutOfRangeException(nameof(accion), "Opcion de respuesta a la flor desconocida.");
    }

    RegistrarActividad();
}
```

### `ResponderContraFlor` (nuevo)

```csharp
public void ResponderContraFlor(ulong jugadorId, bool quiere)
{
    if (jugadorId != TurnoActual)
    {
        throw new InvalidOperationException("No es el turno de este jugador.");
    }

    if (Estado != EstadoRonda.RespondiendoContraFlor)
    {
        throw new InvalidOperationException("No hay ninguna Contra Flor pendiente de responder.");
    }

    var proponente = jugadorId == Jugador1Id ? Jugador2Id : Jugador1Id;

    if (quiere)
    {
        var ganador = CalcularPuntosFlor(Jugador1Id) >= CalcularPuntosFlor(Jugador2Id) ? Jugador1Id : Jugador2Id;
        AsignarPuntos(ganador, PuntosFlorActuales);
    }
    else
    {
        AsignarPuntos(proponente, _puntosFlorAntesDelUltimoAumento);
    }

    FlorCantada[Jugador1Id] = true;
    FlorCantada[Jugador2Id] = true;
    EnvidoCantado = true;

    if (Fase != FaseRonda.Finalizada)
    {
        Estado = EstadoRonda.JugandoCartas;
        TurnoActual = JugadorManoId;
    }

    RegistrarActividad();
}
```

### `IniciarSiguienteMano`

Agregar, junto al resto del reseteo de cantos:

```csharp
PuntosFlorActuales = 0;
_puntosFlorAntesDelUltimoAumento = 0;
```

## `TrucoModule.cs`

### `ConstruirBotonesDeAccion`

Se agregan dos ramas nuevas antes de la rama existente (que pasa a `else if`):

```csharp
private static MessageComponent ConstruirBotonesDeAccion(Ronda ronda)
{
    var botones = new ComponentBuilder()
        .WithButton("🃏 Ver mis cartas", "ver_mano", ButtonStyle.Primary, row: 0);

    if (ronda.Estado == EstadoRonda.RespondiendoFlor)
    {
        var selectFlor = new SelectMenuBuilder()
            .WithCustomId("respuesta_flor")
            .WithPlaceholder("🌸 Responder a la Flor...")
            .AddOption("🌸 La mía es Flor", "la_mia_es_flor")
            .AddOption("🔥 Con Flor Envido", "con_flor_envido")
            .AddOption("☠️ Contra Flor al Resto", "contra_flor_al_resto");

        botones.WithSelectMenu(selectFlor, row: 1);
    }
    else if (ronda.Estado == EstadoRonda.RespondiendoContraFlor)
    {
        botones.WithButton("✅ Quiero", "contraflor_quiero", ButtonStyle.Success, row: 1);
        botones.WithButton("❌ No Quiero", "contraflor_noquiero", ButtonStyle.Danger, row: 1);
    }
    else if (ronda.Estado == EstadoRonda.EsperandoEnvido || ronda.Estado == EstadoRonda.JugandoCartas)
    {
        // ... contenido actual sin cambios ...
    }

    return botones.Build();
}
```

### `SeleccionarEnvido` (rama `"flor"`)

Reemplaza el cuerpo actual de la rama:

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

    if (ronda.Estado == EstadoRonda.RespondiendoFlor)
    {
        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}🌸 ¡<@{Context.User.Id}> cantó Flor! Pero huele a jardín... <@{ronda.TurnoActual}>, ¿qué respondés?",
            components: ConstruirBotonesDeAccion(ronda));
        await DeferAsync();
        return;
    }

    var partidaTerminada = ronda.Fase == FaseRonda.Finalizada;
    var textoFlor = partidaTerminada
        ? $"🌸 ¡<@{Context.User.Id}> cantó FLOR ({ronda.CalcularPuntosFlor(Context.User.Id)} puntos)!"
        : $"🌸 ¡<@{Context.User.Id}> cantó Flor (3 pts)! El Envido se anula. Turno de jugar carta para <@{ronda.TurnoActual}>.";

    if (partidaTerminada)
    {
        await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{textoFlor}");
        await FinalizarYAnunciarRonda(ronda);
    }
    else
    {
        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}{textoFlor}",
            components: ConstruirBotonesDeAccion(ronda));
    }

    await DeferAsync();
    return;
}
```

### `RespuestaFlor` (nuevo handler)

```csharp
[ComponentInteraction("respuesta_flor")]
public async Task RespuestaFlor(string[] opciones)
{
    if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
    {
        await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
        return;
    }

    var puntosJugador1Antes = ronda.PuntosJugador1;
    var puntosJugador2Antes = ronda.PuntosJugador2;

    try
    {
        ronda.ResponderFlor(Context.User.Id, opciones[0]);
    }
    catch (InvalidOperationException ex)
    {
        await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        return;
    }

    if (ronda.Estado == EstadoRonda.RespondiendoContraFlor)
    {
        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}🔥 ¡<@{Context.User.Id}> retrucó la Flor! Turno de <@{ronda.TurnoActual}>.",
            components: ConstruirBotonesDeAccion(ronda));
        await DeferAsync();
        return;
    }

    var deltaJugador1 = ronda.PuntosJugador1 - puntosJugador1Antes;
    var ganador = deltaJugador1 > 0 ? ronda.Jugador1Id : ronda.Jugador2Id;
    var puntosGanados = deltaJugador1 > 0 ? deltaJugador1 : ronda.PuntosJugador2 - puntosJugador2Antes;
    var textoResultado = $"🌸 ¡<@{ganador}> gana el cruce de Flores y se lleva {puntosGanados} puntos!";

    if (ronda.Fase == FaseRonda.Finalizada)
    {
        await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{textoResultado}");
        await FinalizarYAnunciarRonda(ronda);
    }
    else
    {
        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}{textoResultado}",
            components: ConstruirBotonesDeAccion(ronda));
    }

    await DeferAsync();
}
```

### `ResponderContraFlor` (nuevo handler)

```csharp
[ComponentInteraction("contraflor_*")]
public async Task ResponderContraFlor(string accion)
{
    if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
    {
        await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
        return;
    }

    var quiere = accion == "quiero";
    var puntosJugador1Antes = ronda.PuntosJugador1;
    var puntosJugador2Antes = ronda.PuntosJugador2;

    try
    {
        ronda.ResponderContraFlor(Context.User.Id, quiere);
    }
    catch (InvalidOperationException ex)
    {
        await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        return;
    }

    var deltaJugador1 = ronda.PuntosJugador1 - puntosJugador1Antes;
    var ganador = deltaJugador1 > 0 ? ronda.Jugador1Id : ronda.Jugador2Id;
    var puntosGanados = deltaJugador1 > 0 ? deltaJugador1 : ronda.PuntosJugador2 - puntosJugador2Antes;

    var textoResultado = quiere
        ? $"🌸 ¡<@{ganador}> gana el cruce de Flores y se lleva {puntosGanados} puntos!"
        : $"❌ <@{Context.User.Id}> no quiso. <@{ganador}> se lleva {puntosGanados} puntos de Flor.";

    if (ronda.Fase == FaseRonda.Finalizada)
    {
        await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{textoResultado}");
        await FinalizarYAnunciarRonda(ronda);
    }
    else
    {
        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}{textoResultado}",
            components: ConstruirBotonesDeAccion(ronda));
    }

    await DeferAsync();
}
```

## Simplificación aceptada (no se resuelve en este spec)

En todas las resoluciones de Flor, "pasar el turno" usa `TurnoActual = JugadorManoId` (tal como se pidió), no "restaurar el turno de quien estaba por jugar" como sí hace `ResponderEnvido` (`_turnoAntesDelCanto`). Esto es correcto en el caso normal (Flor se canta antes de que nadie juegue carta), pero en el caso borde donde `JugadorManoId` ya jugó su carta de esta baza y el rival canta Flor en su propio turno, resolver la Flor le devolvería el turno a `JugadorManoId` — que ya jugó, y jugaría una segunda carta antes de que el rival juegue la primera. Es un caso borde raro (requiere que se cante Flor después de que el rival del mano ya jugó su carta) y no se resuelve acá — se implementa tal cual se pidió.

## Testing

`tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs` (unitario, sin DB, usando `NuevaRondaConMazoFijo` con `Muestra = new(3, Palo.Oro)`):

- Actualizar el test existente de `CantarFlor` sin cruce para verificar también `Estado == JugandoCartas`, `EnvidoCantado == true`, `TurnoActual == JugadorManoId`.
- `CantarFlor` con cruce (ambos tienen Flor): `Estado == RespondiendoFlor`, `PuntosFlorActuales == 6`, `TurnoActual == rival`.
- `ResponderFlor("la_mia_es_flor")`: el de mayor `CalcularPuntosFlor` gana 6 puntos; `Estado == JugandoCartas`; ambos `FlorCantada` en `true`; `EnvidoCantado == true`.
- `ResponderFlor("con_flor_envido")`: `PuntosFlorActuales == 8`; `Estado == RespondiendoContraFlor`; turno al rival de quien respondió.
- `ResponderFlor("contra_flor_al_resto")`: `PuntosFlorActuales == PuntosObjetivo - max(puntos)`; `Estado == RespondiendoContraFlor`.
- `ResponderContraFlor(quiere: true)` sobre cada ruta de escalada: el de mayor `CalcularPuntosFlor` gana el valor final de `PuntosFlorActuales`.
- `ResponderContraFlor(quiere: false)` sobre cada ruta: quien propuso la escalada gana 6 puntos (el valor previo al aumento), no el valor final.
- Turno/estado incorrecto en `ResponderFlor`/`ResponderContraFlor` tiran `InvalidOperationException`.

No se agregan tests automatizados para los cambios en `TrucoModule.cs` — mismo criterio que el resto de los módulos de interacción en este repo.
