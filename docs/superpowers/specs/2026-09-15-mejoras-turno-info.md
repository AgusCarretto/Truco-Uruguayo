# Turno solo en tu turno, indicador de turno visible, y ocultar puntos de Envido/Flor no resueltos

## Contexto

Cuatro pedidos relacionados con la experiencia de juego de `/truco`:

1. Se puede cantar Truco fuera de turno (bug real).
2. No se ve fácil de quién es el turno en varios lugares.
3. El anuncio de Flor revela el valor exacto de la mano aunque la partida siga.
4. El anuncio de Envido revela los tantos de ambos jugadores aunque se haya rechazado ("No Quiero").

## 1. Bug: se puede gritar Truco fuera de turno

`Ronda.GritarTruco` tiene dos ramas: "escalada tipo tenis" (responder con una subida directa, ya exige `jugadorId == TurnoActual`) y el resto. En "el resto", solo valida `TurnoCantoTruco != null && TurnoCantoTruco != jugadorId` — pero cuando **nadie cantó Truco todavía en esta mano** (`TurnoCantoTruco == null`), no hay ningún chequeo de turno: cualquiera de los dos jugadores puede gritar el primer Truco.

Ojo: no hay que agregar un chequeo de turno genérico a toda la rama — el test existente `GritarTruco_Quiero_LuegoRetruco_SoloElQueRespondioPuedeSubir` prueba deliberadamente que, **después** de que alguien acepta un Truco, quien aceptó puede subir la apuesta (Retruco) aunque en ese momento no sea su `TurnoActual` (el turno vuelve a quien cantó originalmente). Ese comportamiento es correcto y no se toca. El chequeo de turno nuevo va **solo** en el caso `TurnoCantoTruco == null`:

```csharp
if (TurnoCantoTruco is null)
{
    if (jugadorId != TurnoActual)
    {
        throw new InvalidOperationException("Solo podés cantar Truco en tu turno.");
    }
}
else if (TurnoCantoTruco != jugadorId)
{
    throw new InvalidOperationException("Solo el jugador con derecho a subir la apuesta puede cantar.");
}
```

### Efecto colateral: 8 tests existentes quedan con reparto al azar

Ocho tests en `RondaTests.cs` usan el constructor público (`new Ronda(Jugador1, Jugador2, N)`, reparto real al azar) y llaman `ronda.GritarTruco(Jugador1, CantoTruco.Truco)` como primera acción, sin chequear quién quedó de mano. Antes de este chequeo eso daba igual (no había validación); con el chequeo nuevo, esas llamadas fallarían ~50% de las veces según a quién le tocara ser mano al azar — el mismo tipo de test flaky que ya se corrigió una vez con la feature de Flor. Ninguno de estos 8 tests usa el contenido de las cartas para nada (solo la secuencia de cantos de Truco), así que se pasan todos a `NuevaRondaConMazoFijo` (que fija `Jugador1` como mano) con dos manos cualesquiera sin Flor, ya usadas en la corrección anterior: `{1 Espada, 3 Basto, 6 Copa}` y `{7 Oro, 6 Oro, 3 Espada}`.

Los 8 tests (por nombre, todos en `RondaTests.cs`): `ResponderTruco_NoQuieroElRetruco_GanaLaRondaQuienLoCantoConLosPuntosDeTruco`, `IrseAlMazo_TerminaLaRondaYElRivalSeLlevaElValorDeTrucoActual`, `GritarTruco_ActualizaUltimaActividad`, `ResponderTruco_ActualizaUltimaActividad`, `GritarTruco_RivalEscalaDirectoSinDecirQuiero_AceptaElAnteriorYQuedaPendienteElNuevo`, `GritarTruco_EscaladaTenis_ElQueNoDebeResponderNoPuedeEscalar`, `GritarTruco_EscaladaTenis_SaltandoUnNivel_ArrojaYNoCambiaNadaDelEstadoAnterior`, `GritarTruco_EscaladaTenisHastaValeCuatro_TerminaConValorCuatro`.

## 2. Indicador de turno "en todos lados"

`GenerarTextoMarcador` (el cabezal `**MARCADOR** (A N)\n...`) ya se manda al principio de casi todos los mensajes de juego (cada jugada, cada resolución de envido/truco, cada mano nueva). Se le agrega ahí una línea de turno — así aparece automático en todos esos mensajes sin tener que tocarlos uno por uno:

```csharp
private static string GenerarTextoMarcador(Ronda ronda)
{
    var j1 = $"<@{ronda.Jugador1Id}>: {ronda.PuntosJugador1} {GenerarPalitos(ronda.PuntosJugador1)}";
    var j2 = $"<@{ronda.Jugador2Id}>: {ronda.PuntosJugador2} {GenerarPalitos(ronda.PuntosJugador2)}";
    var turno = ronda.Fase == FaseRonda.Finalizada ? "" : $"👉 Turno de <@{ronda.TurnoActual}>\n";
    return $"**MARCADOR** (A {ronda.PuntosObjetivo})\n{j1}\n{j2}\n{turno}\n";
}
```

(se omite la línea de turno cuando la partida ya terminó — mostrar "turno de" justo antes de "🏆 Ronda finalizada" es confuso).

Como consecuencia:

- **`JugarCarta`**: ya usa `GenerarTextoMarcador` y no tenía ninguna indicación de turno en el texto — la gana gratis, sin tocar nada más.
- **`EnviarNuevaRondaAsync`**, **`ResponderEnvidoAsync`** (rama no finalizada) y **`ResponderTrucoAsync`** (rama "truco por N") ya agregaban manualmente `👉 Turno de <@{ronda.TurnoActual}>.` al final del texto — se saca esa parte para no duplicarlo.
- **`AceptarReto`**: ya usa `GenerarTextoMarcador`, pero el texto manual decía `👉 Turno de <@{retadorId}>.` — esto es un bug adicional que aparece al mirar el código: `JugadorManoId` (y por lo tanto `TurnoActual`) se sortea al azar en el constructor de `Ronda`, así que ese texto fijo estaba mal la mitad de las veces (decía que empezaba el retador cuando en realidad podía tocarle al retado). Se saca esa parte también; el marcador ya va a mostrar el turno correcto.
- **`GritarTrucoAsync`**: no usaba `GenerarTextoMarcador` en absoluto (tampoco decía nunca quién debía responder). Se le agrega el prefijo — como `GritarTruco` ya dejó `TurnoActual` en quien debe responder, sale gratis.
- **`SeleccionarEnvido`** (rama envido/real_envido/falta_envido): tampoco usaba `GenerarTextoMarcador`, tenía su propio `Turno de <@{ronda.TurnoActual}> de responder` al final — se reemplaza por el prefijo del marcador (mismo dato, ya consistente con el resto).

`VerMano` (el menú efímero con la mano y los botones de jugar) no pasa por ninguno de estos mensajes — nunca decía de quién era el turno. Se le agrega una línea corta al `text:` de la respuesta:

```csharp
var turnoTexto = ronda.TurnoActual == userId ? "✅ Es tu turno." : $"⏳ Turno de <@{ronda.TurnoActual}>.";
```

pasada como `text: turnoTexto` en el `RespondWithFileAsync` existente.

## 3. Flor: ocultar los puntos mientras la partida sigue

`SeleccionarEnvido` (rama `"flor"`) hoy siempre anuncia `🌸 ¡<@X> cantó FLOR ({puntosFlor} puntos)!` con el valor exacto de `CalcularPuntosFlor`, lo que le revela al rival la composición de la mano aunque el juego continúe. Se muestra el valor exacto **solo** si ese Flor dejó la partida terminada (`ronda.Fase == FaseRonda.Finalizada` después de `CantarFlor`, ya no hay nada que proteger); si no, el mensaje dice simplemente que cantó Flor y ganó los 3 puntos fijos, sin desglose. De paso se unifica en un solo `SendMessageAsync` (antes mandaba dos mensajes separados: el anuncio y, aparte, "Turno de X" + botones — ahora el turno ya sale en el marcador del mismo mensaje):

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

    var partidaTerminada = ronda.Fase == FaseRonda.Finalizada;
    var textoFlor = partidaTerminada
        ? $"🌸 ¡<@{Context.User.Id}> cantó FLOR ({ronda.CalcularPuntosFlor(Context.User.Id)} puntos)!"
        : $"🌸 ¡<@{Context.User.Id}> cantó FLOR! (+3 puntos)";

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

## 4. Envido: ocultar los tantos si no se quiso

`ResponderEnvidoAsync` hoy siempre agrega `Tantos: <@J1> X | <@J2> Y.` al mensaje, sin importar si la respuesta fue Quiero o No Quiero — con "No Quiero" igual se revelan ambos tantos, que es exactamente la información que el envido "no querido" debería proteger. Se muestra el desglose solo cuando se aceptó; si no, el mensaje pasa a ser una frase de rechazo clara sin tantos.

De paso, el cálculo de "cuántos puntos se ganaron" tenía un bug latente: cuando ganaba Jugador2, el código usaba `ronda.PuntosJugador2` (el **total acumulado** de Jugador2 en toda la partida) en vez de la diferencia ganada en este canto puntual — daba un número inflado en cualquier partida donde Jugador2 ya tuviera puntos previos. Se corrige calculando el delta de ambos jugadores, no solo de Jugador1.

```csharp
[ComponentInteraction("resp_envido_*")]
public async Task ResponderEnvidoAsync(string accion)
{
    if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
    {
        await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
        return;
    }

    var cantador = ronda.JugadorQueCanto!.Value;
    var quienResponde = cantador == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;

    if (Context.User.Id != quienResponde)
    {
        await RespondAsync("🚫 No sos quien tiene que responder este canto.", ephemeral: true);
        return;
    }

    var respuesta = accion == "quiero" ? RespuestaCanto.Quiero : RespuestaCanto.NoQuiero;
    var puntosJugador1Antes = ronda.PuntosJugador1;
    var puntosJugador2Antes = ronda.PuntosJugador2;

    try
    {
        ronda.ResponderEnvido(Context.User.Id, respuesta);
    }
    catch (InvalidOperationException ex)
    {
        await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        return;
    }

    var deltaJugador1 = ronda.PuntosJugador1 - puntosJugador1Antes;
    var ganador = deltaJugador1 > 0 ? ronda.Jugador1Id : ronda.Jugador2Id;
    var puntosGanados = deltaJugador1 > 0 ? deltaJugador1 : ronda.PuntosJugador2 - puntosJugador2Antes;

    string mensajePuntos;
    if (respuesta == RespuestaCanto.NoQuiero)
    {
        mensajePuntos = $"❌ <@{quienResponde}> no quiso. <@{ganador}> se lleva {puntosGanados} punto(s) de envido.";
    }
    else
    {
        var puntos1 = ronda.CalcularEnvido(ronda.Jugador1Id);
        var puntos2 = ronda.CalcularEnvido(ronda.Jugador2Id);
        mensajePuntos = $"🎲 <@{ganador}> se lleva {puntosGanados} puntos de envido. Tantos: <@{ronda.Jugador1Id}> {puntos1} | <@{ronda.Jugador2Id}> {puntos2}.";
    }

    if (ronda.Fase == FaseRonda.Finalizada)
    {
        await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{mensajePuntos}");
        await FinalizarYAnunciarRonda(ronda);
    }
    else
    {
        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}{mensajePuntos}",
            components: ConstruirBotonesDeAccion(ronda));
    }

    await DeferAsync();
}
```

(Nota: `cantador` se lee de `ronda.JugadorQueCanto` **antes** de llamar a `ResponderEnvido`, aunque esa propiedad no se limpia ahí — se podría leer después también, pero leerlo antes dejaba el código más parecido al original y evita cualquier duda sobre en qué momento se resetea.)

## Nota sobre el pedido de Flor sin resolver

El pedido original decía, sobre la Flor: *"si uno tiene y el otro no, que el otro diga tiene, un botón o algo"*. Se interpretó como una referencia a que el rival ya se entera de que se cantó Flor por el anuncio del canal (comportamiento ya existente) — **no** se agrega ningún mecanismo nuevo de respuesta/quiero-no-quiero a la Flor, siguiendo la decisión explícita anterior de mantenerla "directa" (sin escalada). Si la intención era otra, es un ajuste a futuro.

## Testing

`tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`:

- Dos tests nuevos para el chequeo de turno en el primer Truco de la mano (uno que falla fuera de turno, uno que funciona en turno).
- Los 8 tests listados en la sección 1 pasan a usar `NuevaRondaConMazoFijo` en vez del constructor público.

No se agregan tests automatizados para los cambios en `TrucoModule.cs` (comandos/componentes de Discord) — mismo criterio que el resto de los módulos de interacción en este repo. Se verifica con `dotnet build` + la suite completa + prueba manual.
