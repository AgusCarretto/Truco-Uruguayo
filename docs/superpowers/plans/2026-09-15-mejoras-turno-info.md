# Turno correcto, indicador de turno e info oculta en Envido/Flor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Truco solo se puede cantar en turno; el turno queda visible en (casi) todos los mensajes del juego; Flor y Envido dejan de revelar puntos cuando no corresponde.

**Architecture:** El chequeo de turno para el primer Truco de la mano se agrega dentro de `Ronda.GritarTruco` (mismo patrón que el resto de las validaciones de juego). El indicador de turno se centraliza en `GenerarTextoMarcador` (ya usado en casi todos los mensajes), eliminando los "Turno de X" manuales que quedan duplicados. Los cambios de Flor/Envido son puramente de qué texto arma `TrucoModule` según el resultado de la llamada a `Ronda`.

**Tech Stack:** C# / .NET 10, xUnit (tests de `Ronda` son unitarios puros, sin DB).

## Global Constraints

- El chequeo de turno nuevo en `GritarTruco` va **solo** cuando `TurnoCantoTruco is null` (nadie cantó Truco todavía en esta mano) — no toca el caso ya testeado donde quien aceptó un Truco puede subir la apuesta después aunque no sea su `TurnoActual`.
- `GenerarTextoMarcador` incluye `👉 Turno de <@X>` salvo que `ronda.Fase == FaseRonda.Finalizada`. Cualquier lugar que ya agregaba manualmente "Turno de X" después de usar `GenerarTextoMarcador` se lo saca (queda duplicado si no).
- Flor: se muestra `CalcularPuntosFlor` solo si ese canto dejó `ronda.Fase == FaseRonda.Finalizada`; si no, el mensaje dice "cantó FLOR! (+3 puntos)" sin desglose.
- Envido: los tantos (`Tantos: ...`) solo se muestran si la respuesta fue `Quiero`; con `NoQuiero` el mensaje es `"❌ <@respondedor> no quiso. <@ganador> se lleva N punto(s) de envido."` sin tantos. El cálculo de puntos ganados usa el delta de **ambos** jugadores (no el total acumulado de Jugador2, que era un bug existente).
- No se agrega ningún mecanismo nuevo de respuesta a la Flor (sin quiero/no quiero) — se mantiene "directa", como se decidió en la feature anterior.

Spec completo: `docs/superpowers/specs/2026-09-15-mejoras-turno-info.md`

---

### Task 1: `Ronda.GritarTruco` exige turno para el primer Truco de la mano

**Files:**
- Modify: `src/TrucoUruguayo.Core/Juego/Ronda.cs:207-218`
- Test: `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs` (tests nuevos + 8 tests existentes que hay que pasar a mano fija)

**Interfaces:**
- Consumes: nada nuevo.
- Produces: sin cambios de firma — `GritarTruco` sigue siendo `public void GritarTruco(ulong jugadorId, CantoTruco canto)`, solo cambia cuándo tira `InvalidOperationException`.

- [ ] **Step 1: Escribir los tests que fallan**

Agregar en `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`, justo antes de `// --- Cantos (Truco) ---`:

```csharp
    [Fact]
    public void GritarTruco_PrimerTrucoDeLaManoFueraDeTurno_TiraExcepcion()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        // Jugador1 es mano (le toca a el); Jugador2 intenta cantar Truco primero.
        var excepcion = Record.Exception(() => ronda.GritarTruco(Jugador2, CantoTruco.Truco));

        Assert.IsType<InvalidOperationException>(excepcion);
    }

    [Fact]
    public void GritarTruco_PrimerTrucoDeLaManoEnTurno_Funciona()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        var excepcion = Record.Exception(() => ronda.GritarTruco(Jugador1, CantoTruco.Truco));

        Assert.Null(excepcion);
    }

```

- [ ] **Step 2: Correr los tests y verificar que falla el primero**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests --filter "FullyQualifiedName~GritarTruco_PrimerTrucoDeLaMano"`
Expected: `GritarTruco_PrimerTrucoDeLaManoFueraDeTurno_TiraExcepcion` FALLA (hoy no tira ninguna excepción), `GritarTruco_PrimerTrucoDeLaManoEnTurno_Funciona` pasa (ya funcionaba).

- [ ] **Step 3: Implementar el chequeo**

En `src/TrucoUruguayo.Core/Juego/Ronda.cs`, reemplazar:

```csharp
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
```

por:

```csharp
        else
        {
            if (Estado != EstadoRonda.JugandoCartas && Estado != EstadoRonda.EsperandoEnvido)
            {
                throw new InvalidOperationException("No se puede cantar truco en este momento.");
            }

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
        }
```

- [ ] **Step 4: Correr los tests nuevos y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests --filter "FullyQualifiedName~GritarTruco_PrimerTrucoDeLaMano"`
Expected: PASS (2 tests).

- [ ] **Step 5: Correr toda la suite — van a fallar 8 tests existentes por reparto al azar**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests`
Expected: FALLAN estos 8 (usan `new Ronda(Jugador1, Jugador2, N)` con reparto real al azar y llaman `GritarTruco(Jugador1, ...)` como primera acción sin chequear quién quedó de mano — con el chequeo nuevo eso ahora depende del sorteo): `ResponderTruco_NoQuieroElRetruco_GanaLaRondaQuienLoCantoConLosPuntosDeTruco`, `IrseAlMazo_TerminaLaRondaYElRivalSeLlevaElValorDeTrucoActual`, `GritarTruco_ActualizaUltimaActividad`, `ResponderTruco_ActualizaUltimaActividad`, `GritarTruco_RivalEscalaDirectoSinDecirQuiero_AceptaElAnteriorYQuedaPendienteElNuevo`, `GritarTruco_EscaladaTenis_ElQueNoDebeResponderNoPuedeEscalar`, `GritarTruco_EscaladaTenis_SaltandoUnNivel_ArrojaYNoCambiaNadaDelEstadoAnterior`, `GritarTruco_EscaladaTenisHastaValeCuatro_TerminaConValorCuatro`. (Pueden pasar por casualidad en una corrida individual porque dependen del azar — no importa, se corrigen igual.)

- [ ] **Step 6: Pasar esos 8 tests a mano fija (`NuevaRondaConMazoFijo`)**

Ninguno de estos tests usa el contenido de las cartas — solo la secuencia de cantos de Truco — así que alcanza con cambiar la línea de construcción de `Ronda` por `NuevaRondaConMazoFijo` con dos manos sin Flor, sin tocar nada más del cuerpo del test.

En `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`, reemplazar:

```csharp
    public void ResponderTruco_NoQuieroElRetruco_GanaLaRondaQuienLoCantoConLosPuntosDeTruco()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
        ronda.ResponderTruco(Jugador1, RespuestaCanto.NoQuiero);

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador2, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
    }
```

por:

```csharp
    public void ResponderTruco_NoQuieroElRetruco_GanaLaRondaQuienLoCantoConLosPuntosDeTruco()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) },
            puntosObjetivo: 2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
        ronda.ResponderTruco(Jugador1, RespuestaCanto.NoQuiero);

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador2, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
    }
```

Reemplazar:

```csharp
    public void IrseAlMazo_TerminaLaRondaYElRivalSeLlevaElValorDeTrucoActual()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.IrseAlMazo(Jugador1);

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador2, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
        Assert.Throws<InvalidOperationException>(() => ronda.IrseAlMazo(Jugador2));
    }
```

por:

```csharp
    public void IrseAlMazo_TerminaLaRondaYElRivalSeLlevaElValorDeTrucoActual()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) },
            puntosObjetivo: 2);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        ronda.IrseAlMazo(Jugador1);

        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador2, ronda.GanadorRonda);
        Assert.Equal(2, ronda.PuntosJugador2);
        Assert.Equal(0, ronda.PuntosJugador1);
        Assert.Throws<InvalidOperationException>(() => ronda.IrseAlMazo(Jugador2));
    }
```

Reemplazar:

```csharp
    public void GritarTruco_ActualizaUltimaActividad()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        var antes = DateTime.UtcNow;

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);

        Assert.True(ronda.UltimaActividad >= antes);
    }
```

por:

```csharp
    public void GritarTruco_ActualizaUltimaActividad()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });
        var antes = DateTime.UtcNow;

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);

        Assert.True(ronda.UltimaActividad >= antes);
    }
```

Reemplazar:

```csharp
    public void ResponderTruco_ActualizaUltimaActividad()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        var antes = DateTime.UtcNow;

        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        Assert.True(ronda.UltimaActividad >= antes);
    }
```

por:

```csharp
    public void ResponderTruco_ActualizaUltimaActividad()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        var antes = DateTime.UtcNow;

        ronda.ResponderTruco(Jugador2, RespuestaCanto.Quiero);

        Assert.True(ronda.UltimaActividad >= antes);
    }
```

Reemplazar:

```csharp
    public void GritarTruco_RivalEscalaDirectoSinDecirQuiero_AceptaElAnteriorYQuedaPendienteElNuevo()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        // Jugador2 es quien debe responder. En vez de "Quiero", escala directo a Retruco.
        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
```

por:

```csharp
    public void GritarTruco_RivalEscalaDirectoSinDecirQuiero_AceptaElAnteriorYQuedaPendienteElNuevo()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        // Jugador2 es quien debe responder. En vez de "Quiero", escala directo a Retruco.
        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
```

Reemplazar:

```csharp
    public void GritarTruco_EscaladaTenis_ElQueNoDebeResponderNoPuedeEscalar()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);

        // Jugador1 (quien ya cantó) intenta escalar de nuevo antes de que Jugador2 responda.
        Assert.Throws<InvalidOperationException>(() => ronda.GritarTruco(Jugador1, CantoTruco.Retruco));
    }
```

por:

```csharp
    public void GritarTruco_EscaladaTenis_ElQueNoDebeResponderNoPuedeEscalar()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);

        // Jugador1 (quien ya cantó) intenta escalar de nuevo antes de que Jugador2 responda.
        Assert.Throws<InvalidOperationException>(() => ronda.GritarTruco(Jugador1, CantoTruco.Retruco));
    }
```

Reemplazar:

```csharp
    public void GritarTruco_EscaladaTenis_SaltandoUnNivel_ArrojaYNoCambiaNadaDelEstadoAnterior()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
```

por:

```csharp
    public void GritarTruco_EscaladaTenis_SaltandoUnNivel_ArrojaYNoCambiaNadaDelEstadoAnterior()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
```

Reemplazar:

```csharp
    public void GritarTruco_EscaladaTenisHastaValeCuatro_TerminaConValorCuatro()
    {
        var ronda = new Ronda(Jugador1, Jugador2, 15);

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
        ronda.GritarTruco(Jugador1, CantoTruco.ValeCuatro);
```

por:

```csharp
    public void GritarTruco_EscaladaTenisHastaValeCuatro_TerminaConValorCuatro()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        ronda.GritarTruco(Jugador1, CantoTruco.Truco);
        ronda.GritarTruco(Jugador2, CantoTruco.Retruco);
        ronda.GritarTruco(Jugador1, CantoTruco.ValeCuatro);
```

- [ ] **Step 7: Correr toda la suite y verificar que pasa, varias veces**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests` (correrlo 3 veces seguidas — sin repartos al azar de por medio en los tests de Truco, no debería haber diferencia entre corridas)
Expected: PASS las 3 veces, misma cantidad de tests en las 3.

- [ ] **Step 8: Commit**

```bash
git add src/TrucoUruguayo.Core/Juego/Ronda.cs tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs
git commit -m "$(cat <<'EOF'
fix: exige turno para el primer Truco de la mano

Corrige tambien 8 tests preexistentes que usaban reparto al azar y
llamaban GritarTruco como primera accion sin chequear quien quedo de
mano, lo que ahora los volvia flaky con el chequeo nuevo.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Indicador de turno centralizado en `GenerarTextoMarcador`

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`

**Interfaces:**
- Consumes: `Ronda.TurnoActual`, `Ronda.Fase`, `FaseRonda.Finalizada` (todos ya existentes).
- Produces: `GenerarTextoMarcador` sigue siendo `private static string GenerarTextoMarcador(Ronda ronda)` — mismo nombre y firma, usado por todo el resto del archivo sin cambios en los call sites (salvo sacar los "Turno de X" ahora duplicados).

Sin tests automatizados (handlers de Discord). Se verifica con `dotnet build` + suite completa + prueba manual.

- [ ] **Step 1: Agregar la línea de turno a `GenerarTextoMarcador`**

En `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`, reemplazar:

```csharp
    private static string GenerarTextoMarcador(Ronda ronda)
    {
        var j1 = $"<@{ronda.Jugador1Id}>: {ronda.PuntosJugador1} {GenerarPalitos(ronda.PuntosJugador1)}";
        var j2 = $"<@{ronda.Jugador2Id}>: {ronda.PuntosJugador2} {GenerarPalitos(ronda.PuntosJugador2)}";
        return $"**MARCADOR** (A {ronda.PuntosObjetivo})\n{j1}\n{j2}\n\n";
    }
```

por:

```csharp
    private static string GenerarTextoMarcador(Ronda ronda)
    {
        var j1 = $"<@{ronda.Jugador1Id}>: {ronda.PuntosJugador1} {GenerarPalitos(ronda.PuntosJugador1)}";
        var j2 = $"<@{ronda.Jugador2Id}>: {ronda.PuntosJugador2} {GenerarPalitos(ronda.PuntosJugador2)}";
        var turno = ronda.Fase == FaseRonda.Finalizada ? "" : $"👉 Turno de <@{ronda.TurnoActual}>\n";
        return $"**MARCADOR** (A {ronda.PuntosObjetivo})\n{j1}\n{j2}\n{turno}\n";
    }
```

- [ ] **Step 2: Sacar el "Turno de X" manual, ahora duplicado, de `EnviarNuevaRondaAsync`**

Reemplazar:

```csharp
    private async Task EnviarNuevaRondaAsync(Ronda ronda)
    {
        await using var streamMesaNueva = await _generadorImagenes.GenerarMesaActualAsync(ronda.Muestra, null, null);
        await Context.Channel.SendFileAsync(
            streamMesaNueva,
            "mesa.png",
            text: $"{GenerarTextoMarcador(ronda)}🔄 Nueva ronda, reparte las cartas... La nueva muestra es **{ronda.Muestra}**. 👉 Turno de <@{ronda.TurnoActual}>.",
            components: ConstruirBotonesDeAccion(ronda));
    }
```

por:

```csharp
    private async Task EnviarNuevaRondaAsync(Ronda ronda)
    {
        await using var streamMesaNueva = await _generadorImagenes.GenerarMesaActualAsync(ronda.Muestra, null, null);
        await Context.Channel.SendFileAsync(
            streamMesaNueva,
            "mesa.png",
            text: $"{GenerarTextoMarcador(ronda)}🔄 Nueva ronda, reparte las cartas... La nueva muestra es **{ronda.Muestra}**.",
            components: ConstruirBotonesDeAccion(ronda));
    }
```

- [ ] **Step 3: Sacar el "Turno de X" manual de `AceptarReto` (y de paso corregir que siempre decía que arrancaba el retador)**

`JugadorManoId` (y por lo tanto `TurnoActual`) se sortea al azar en el constructor de `Ronda` — el texto fijo `Turno de <@{retadorId}>` estaba mal la mitad de las veces. Reemplazar:

```csharp
        await using var streamMesa = await _generadorImagenes.GenerarMesaActualAsync(ronda.Muestra, null, null);
        await Context.Channel.SendFileAsync(
            streamMesa,
            "mesa.png",
            text: $"{GenerarTextoMarcador(ronda)}🎉 ¡Partida a {puntos} puntos iniciada por 🪙 {apuesta} monedas! 🃏 La muestra es **{ronda.Muestra}**. 👉 Turno de <@{retadorId}>.",
            components: ConstruirBotonesDeAccion(ronda));
```

por:

```csharp
        await using var streamMesa = await _generadorImagenes.GenerarMesaActualAsync(ronda.Muestra, null, null);
        await Context.Channel.SendFileAsync(
            streamMesa,
            "mesa.png",
            text: $"{GenerarTextoMarcador(ronda)}🎉 ¡Partida a {puntos} puntos iniciada por 🪙 {apuesta} monedas! 🃏 La muestra es **{ronda.Muestra}**.",
            components: ConstruirBotonesDeAccion(ronda));
```

- [ ] **Step 4: Agregar el marcador (con turno) al aviso de "gritó Truco"**

Reemplazar:

```csharp
        await Context.Channel.SendMessageAsync(
            $"🔥 <@{Context.User.Id}> gritó **{NombreCantoTruco(canto)}**!",
            components: botones.Build());
```

por:

```csharp
        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}🔥 <@{Context.User.Id}> gritó **{NombreCantoTruco(canto)}**!",
            components: botones.Build());
```

- [ ] **Step 5: Reemplazar el "Turno de X de responder" manual del canto de Envido por el marcador**

Reemplazar:

```csharp
        await Context.Channel.SendMessageAsync(
            $"🎲 ¡<@{Context.User.Id}> cantó {nombreEnvido}! Turno de <@{ronda.TurnoActual}> de responder.",
            components: botones);
```

por:

```csharp
        await Context.Channel.SendMessageAsync(
            $"{GenerarTextoMarcador(ronda)}🎲 ¡<@{Context.User.Id}> cantó {nombreEnvido}!",
            components: botones);
```

- [ ] **Step 6: Sacar el "Turno de X" manual de `ResponderTrucoAsync`**

Reemplazar:

```csharp
            await Context.Channel.SendMessageAsync(
                $"{GenerarTextoMarcador(ronda)}🔥 ¡Truco por {ronda.ValorTrucoActual}! 👉 Turno de <@{ronda.TurnoActual}>.",
                components: ConstruirBotonesDeAccion(ronda));
```

por:

```csharp
            await Context.Channel.SendMessageAsync(
                $"{GenerarTextoMarcador(ronda)}🔥 ¡Truco por {ronda.ValorTrucoActual}!",
                components: ConstruirBotonesDeAccion(ronda));
```

- [ ] **Step 7: Compilar y correr toda la suite**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && dotnet test tests/TrucoUruguayo.Core.Tests && dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: build sin errores, toda la suite en verde (el `ResponderEnvidoAsync` todavía tiene su propio "Turno de X" manual, que se toca recién en la Task 5 — dejarlo así por ahora no rompe nada, solo va a quedar duplicado un rato).

- [ ] **Step 8: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs
git commit -m "$(cat <<'EOF'
feat: centraliza el indicador de turno en el marcador

De paso corrige que el mensaje de partida iniciada siempre decia que
arrancaba el retador, cuando en realidad JugadorManoId se sortea al
azar.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Turno visible al ver la mano

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`

**Interfaces:**
- Consumes: `Ronda.TurnoActual` (ya existente).
- Produces: nada nuevo consumido por otras tasks.

Sin tests automatizados. Se verifica con `dotnet build` + suite completa + prueba manual.

- [ ] **Step 1: Agregar la línea de turno al menú de "Ver mis cartas"**

En `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`, reemplazar:

```csharp
        componentes.WithButton("📊 Orden de las cartas", "ayuda_cartas", ButtonStyle.Secondary, row: 4);

        await using var streamMano = await _generadorImagenes.GenerarManoAsync(mano);
        await RespondWithFileAsync(streamMano, "mano.png", components: componentes.Build(), ephemeral: true);
    }
```

por:

```csharp
        componentes.WithButton("📊 Orden de las cartas", "ayuda_cartas", ButtonStyle.Secondary, row: 4);

        var turnoTexto = ronda.TurnoActual == userId ? "✅ Es tu turno." : $"⏳ Turno de <@{ronda.TurnoActual}>.";

        await using var streamMano = await _generadorImagenes.GenerarManoAsync(mano);
        await RespondWithFileAsync(streamMano, "mano.png", text: turnoTexto, components: componentes.Build(), ephemeral: true);
    }
```

- [ ] **Step 2: Compilar y correr toda la suite**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && dotnet test tests/TrucoUruguayo.Core.Tests && dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: build sin errores, toda la suite en verde.

- [ ] **Step 3: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs
git commit -m "$(cat <<'EOF'
feat: muestra de quien es el turno al ver la mano

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Ocultar los puntos de Flor mientras la partida sigue

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`

**Interfaces:**
- Consumes: `Ronda.CantarFlor(ulong)`, `Ronda.CalcularPuntosFlor(ulong) -> int`, `Ronda.Fase`, `FaseRonda.Finalizada` (todos ya existentes).
- Produces: nada nuevo consumido por otras tasks.

Sin tests automatizados. Se verifica con `dotnet build` + suite completa + prueba manual.

- [ ] **Step 1: Ocultar `CalcularPuntosFlor` salvo que la partida haya terminado**

En `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`, reemplazar el bloque de la rama `"flor"` dentro de `SeleccionarEnvido`:

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

por:

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

- [ ] **Step 2: Compilar y correr toda la suite**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && dotnet test tests/TrucoUruguayo.Core.Tests && dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: build sin errores, toda la suite en verde.

- [ ] **Step 3: Prueba manual**

Con el bot corriendo: cantar Flor sin que eso termine la partida — el mensaje debe decir "cantó FLOR! (+3 puntos)" sin ningún otro número. Forzar (con puntos objetivo bajo) que el Flor deje a alguien en el objetivo — ahí sí debe mostrar el desglose exacto de `CalcularPuntosFlor` antes de anunciar el fin de la partida.

- [ ] **Step 4: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs
git commit -m "$(cat <<'EOF'
feat: oculta los puntos exactos de Flor mientras la partida sigue

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Ocultar los tantos de Envido si no se quiso

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`

**Interfaces:**
- Consumes: `Ronda.JugadorQueCanto`, `Ronda.ResponderEnvido(ulong, RespuestaCanto)`, `Ronda.PuntosJugador1`, `Ronda.PuntosJugador2`, `Ronda.CalcularEnvido(ulong) -> int` (todos ya existentes).
- Produces: nada nuevo consumido por otras tasks.

Sin tests automatizados. Se verifica con `dotnet build` + suite completa + prueba manual.

- [ ] **Step 1: Reescribir `ResponderEnvidoAsync`**

En `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`, reemplazar el método completo:

```csharp
    [ComponentInteraction("resp_envido_*")]
    public async Task ResponderEnvidoAsync(string accion)
    {
        if (!_gestorPartidas.PartidasActivas.TryGetValue(Context.Channel.Id, out var ronda))
        {
            await RespondAsync("❌ No hay una partida activa en este canal.", ephemeral: true);
            return;
        }

        var quienResponde = ronda.JugadorQueCanto == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;

        if (Context.User.Id != quienResponde)
        {
            await RespondAsync("🚫 No sos quien tiene que responder este canto.", ephemeral: true);
            return;
        }

        var respuesta = accion == "quiero" ? RespuestaCanto.Quiero : RespuestaCanto.NoQuiero;
        var puntosJugador1Antes = ronda.PuntosJugador1;

        try
        {
            ronda.ResponderEnvido(Context.User.Id, respuesta);
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
            return;
        }

        var puntosGanadosJugador1 = ronda.PuntosJugador1 - puntosJugador1Antes;
        var mensajePuntos = puntosGanadosJugador1 > 0
            ? $"🎲 <@{ronda.Jugador1Id}> se lleva {puntosGanadosJugador1} puntos de envido."
            : $"🎲 <@{ronda.Jugador2Id}> se lleva {ronda.PuntosJugador2} puntos de envido.";

        var puntos1 = ronda.CalcularEnvido(ronda.Jugador1Id);
        var puntos2 = ronda.CalcularEnvido(ronda.Jugador2Id);
        mensajePuntos += $" Tantos: <@{ronda.Jugador1Id}> {puntos1} | <@{ronda.Jugador2Id}> {puntos2}.";

        if (ronda.Fase == FaseRonda.Finalizada)
        {
            await Context.Channel.SendMessageAsync($"{GenerarTextoMarcador(ronda)}{mensajePuntos}");
            await FinalizarYAnunciarRonda(ronda);
        }
        else
        {
            await Context.Channel.SendMessageAsync(
                $"{GenerarTextoMarcador(ronda)}{mensajePuntos} 👉 Turno de <@{ronda.TurnoActual}>.",
                components: ConstruirBotonesDeAccion(ronda));
        }

        await DeferAsync();
    }
```

por:

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

- [ ] **Step 2: Compilar y correr toda la suite**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && dotnet test tests/TrucoUruguayo.Core.Tests && dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: build sin errores, toda la suite en verde.

- [ ] **Step 3: Prueba manual**

Con el bot corriendo: cantar Envido y responder "Quiero" — el mensaje debe mostrar "Tantos: ..." de ambos. Cantar Envido de nuevo (otra mano) y responder "No Quiero" — el mensaje debe decir solo "❌ fulano no quiso. Mengano se lleva 1 punto de envido." sin ningún tanto.

- [ ] **Step 4: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs
git commit -m "$(cat <<'EOF'
feat: oculta los tantos de envido si se responde No Quiero

De paso corrige un bug donde, si ganaba Jugador2, el mensaje mostraba
su puntaje total acumulado en vez de los puntos ganados en ese canto.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
