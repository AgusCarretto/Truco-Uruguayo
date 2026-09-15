# Cruce de Flores (Flor contra Flor) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Si el rival también tiene Flor, se abre un cruce (`Flor` → opcional `Con Flor Envido`/`Contra Flor al Resto` → `Quiero`/`No Quiero`); si no tiene, el Envido queda anulado para esa mano.

**Architecture:** Dos estados nuevos en `EstadoRonda` (`RespondiendoFlor`, `RespondiendoContraFlor`) y dos métodos nuevos en `Ronda` (`ResponderFlor`, `ResponderContraFlor`) siguiendo el mismo patrón que `CantarEnvido`/`ResponderEnvido`/`GritarTruco`/`ResponderTruco`: validan turno y estado, mutan puntaje/estado, tiran `InvalidOperationException` si algo no corresponde. `TrucoModule` agrega dos ramas a `ConstruirBotonesDeAccion` y dos handlers nuevos, siguiendo el patrón ya usado para Envido/Truco (comparar puntos antes/después de la llamada para saber quién ganó, en vez de agregar una propiedad nueva a `Ronda`).

**Tech Stack:** C# / .NET 10, xUnit (tests de `Ronda` son unitarios puros, sin DB — usan `NuevaRondaConMazoFijo`, con `Muestra = new(3, Palo.Oro)`).

## Global Constraints

- `EstadoRonda` gana `RespondiendoFlor` y `RespondiendoContraFlor`.
- `PuntosFlorActuales` (nueva propiedad, `int`, `get; private set;`) arranca en 0 y se resetea a 0 en `IniciarSiguienteMano` junto con el resto del estado de cantos de la mano.
- Cruce (ambos tienen Flor): `PuntosFlorActuales = 6`. `"con_flor_envido"` suma +2. `"contra_flor_al_resto"` lo pone en `PuntosObjetivo - Math.Max(PuntosJugador1, PuntosJugador2)`.
- `EnvidoCantado = true` se marca en **todo** punto donde la Flor queda resuelta (sin cruce, `"la_mia_es_flor"`, y ambas ramas de `ResponderContraFlor`) — es lo que de verdad anula el Envido (`PuedeCantarEnvido` depende de esa propiedad, no de `Estado`).
- En el cruce, al resolverse, **ambos** `FlorCantada[Jugador1Id]` y `FlorCantada[Jugador2Id]` quedan en `true` (los dos tenían Flor).
- "No Quiero" en `ResponderContraFlor` premia a quien **propuso** la escalada (no a quien responde) con el valor de `PuntosFlorActuales` **antes** del último aumento (siempre 6 en esta escalada de un solo nivel, guardado en un campo privado en vez de hardcodeado).
- Todas las resoluciones de Flor mandan el turno a `JugadorManoId` (tal como se pidió — ver la limitación aceptada en el spec, no se resuelve en este plan).
- No se tocan `CantarEnvido`, `JugarCarta` ni `GritarTruco`: sus chequeos actuales ("tenés Flor sin cantar", validaciones de `Estado`) ya bloquean cualquier interrupción de un cruce de Flor pendiente.

Spec completo: `docs/superpowers/specs/2026-09-15-flor-cruzada.md`

---

### Task 1: Estados nuevos, `PuntosFlorActuales`, y reescribir `CantarFlor`

**Files:**
- Modify: `src/TrucoUruguayo.Core/Juego/EstadoRonda.cs`
- Modify: `src/TrucoUruguayo.Core/Juego/Ronda.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`

**Interfaces:**
- Consumes: `TieneFlor(ulong) -> bool`, `CalcularPuntosFlor(ulong) -> int`, `AsignarPuntos(ulong, int)`, `PuedeCantarEnvido(ulong) -> bool` (todos ya existentes).
- Produces (usado por Task 2 y 3): `EstadoRonda.RespondiendoFlor`, `EstadoRonda.RespondiendoContraFlor`, `Ronda.PuntosFlorActuales` (`int`, público), el campo privado `_puntosFlorAntesDelUltimoAumento` (usado en Task 3).

- [ ] **Step 1: Escribir los tests que fallan**

En `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`, reemplazar el test existente:

```csharp
    public void CantarFlor_ConFlor_MarcaFlorCantadaYSumaTresPuntos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        ronda.CantarFlor(Jugador1);

        Assert.True(ronda.FlorCantada[Jugador1]);
        Assert.Equal(3, ronda.PuntosJugador1);
    }
```

por:

```csharp
    public void CantarFlor_ConFlor_MarcaFlorCantadaYSumaTresPuntos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) });

        ronda.CantarFlor(Jugador1);

        Assert.True(ronda.FlorCantada[Jugador1]);
        Assert.Equal(3, ronda.PuntosJugador1);
        Assert.Equal(EstadoRonda.JugandoCartas, ronda.Estado);
        Assert.True(ronda.EnvidoCantado);
        Assert.Equal(Jugador1, ronda.TurnoActual);
    }

    [Fact]
    public void CantarFlor_RivalTambienTieneFlor_AbreElCruce()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) });

        ronda.CantarFlor(Jugador1);

        Assert.Equal(EstadoRonda.RespondiendoFlor, ronda.Estado);
        Assert.Equal(6, ronda.PuntosFlorActuales);
        Assert.Equal(Jugador2, ronda.TurnoActual);
        Assert.Equal(0, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests --filter "FullyQualifiedName~CantarFlor_ConFlor_MarcaFlorCantadaYSumaTresPuntos|FullyQualifiedName~CantarFlor_RivalTambienTieneFlor_AbreElCruce"`
Expected: FAIL — `CantarFlor_RivalTambienTieneFlor_AbreElCruce` no compila (`Ronda` no tiene `PuntosFlorActuales`, `EstadoRonda` no tiene `RespondiendoFlor`); `CantarFlor_ConFlor_MarcaFlorCantadaYSumaTresPuntos` compila pero falla en las aserciones nuevas (`Estado` sigue en `EsperandoEnvido`, `EnvidoCantado` sigue en `false`).

- [ ] **Step 3: Implementar**

En `src/TrucoUruguayo.Core/Juego/EstadoRonda.cs`, reemplazar:

```csharp
public enum EstadoRonda
{
    EsperandoEnvido,
    RespondiendoCanto,
    JugandoCartas,
    RespondiendoTruco,
}
```

por:

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

En `src/TrucoUruguayo.Core/Juego/Ronda.cs`, agregar el campo privado junto a `_turnoAntesDelTruco`:

```csharp
    private ulong _turnoAntesDelTruco;
    private int _puntosFlorAntesDelUltimoAumento;
```

Agregar la propiedad pública junto a `FlorCantada`:

```csharp
    public Dictionary<ulong, bool> FlorCantada { get; private set; }
    public int PuntosFlorActuales { get; private set; }
```

Reemplazar el método `CantarFlor` completo:

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

por:

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

En `IniciarSiguienteMano`, reemplazar:

```csharp
        TurnoCantoTruco = null;
        FlorCantada.Clear();
```

por:

```csharp
        TurnoCantoTruco = null;
        FlorCantada.Clear();
        PuntosFlorActuales = 0;
        _puntosFlorAntesDelUltimoAumento = 0;
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests --filter "FullyQualifiedName~CantarFlor_ConFlor_MarcaFlorCantadaYSumaTresPuntos|FullyQualifiedName~CantarFlor_RivalTambienTieneFlor_AbreElCruce"`
Expected: PASS (2 tests).

- [ ] **Step 5: Correr toda la suite y verificar que no se rompió nada**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests`
Expected: PASS (toda la suite).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Core/Juego/EstadoRonda.cs src/TrucoUruguayo.Core/Juego/Ronda.cs tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs
git commit -m "$(cat <<'EOF'
feat: CantarFlor abre un cruce si el rival tambien tiene Flor

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `Ronda.ResponderFlor`

**Files:**
- Modify: `src/TrucoUruguayo.Core/Juego/Ronda.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`

**Interfaces:**
- Consumes (de Task 1): `EstadoRonda.RespondiendoFlor`, `EstadoRonda.RespondiendoContraFlor`, `PuntosFlorActuales`, `_puntosFlorAntesDelUltimoAumento`, `Ronda.CantarFlor(ulong)`.
- Produces (usado por Task 3 y 4): `Ronda.ResponderFlor(ulong jugadorId, string accion)` (`void`, tira `InvalidOperationException` si no corresponde; `accion` es `"la_mia_es_flor"`, `"con_flor_envido"` o `"contra_flor_al_resto"`).

- [ ] **Step 1: Escribir los tests que fallan**

Agregar en `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`, después de `CantarFlor_RivalTambienTieneFlor_AbreElCruce`:

```csharp
    [Fact]
    public void ResponderFlor_LaMiaEsFlor_GanaElDeMasPuntosYSumaSeisPuntos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.CantarFlor(Jugador1);

        ronda.ResponderFlor(Jugador2, "la_mia_es_flor");

        // CalcularPuntosFlor(Jugador1) = 47 (2+4+5 de Oro), CalcularPuntosFlor(Jugador2) = 37
        // (Caballo+Sota de Oro + 3 de Espada) — gana Jugador1.
        Assert.Equal(6, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
        Assert.Equal(EstadoRonda.JugandoCartas, ronda.Estado);
        Assert.True(ronda.FlorCantada[Jugador1]);
        Assert.True(ronda.FlorCantada[Jugador2]);
        Assert.True(ronda.EnvidoCantado);
        Assert.Equal(Jugador1, ronda.TurnoActual);
    }

    [Fact]
    public void ResponderFlor_ConFlorEnvido_SubeDosPuntosYPasaAContraFlor()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.CantarFlor(Jugador1);

        ronda.ResponderFlor(Jugador2, "con_flor_envido");

        Assert.Equal(8, ronda.PuntosFlorActuales);
        Assert.Equal(EstadoRonda.RespondiendoContraFlor, ronda.Estado);
        Assert.Equal(Jugador1, ronda.TurnoActual);
    }

    [Fact]
    public void ResponderFlor_ContraFlorAlResto_PoneElRestoYPasaAContraFlor()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) },
            puntosObjetivo: 15);
        ronda.CantarFlor(Jugador1);

        ronda.ResponderFlor(Jugador2, "contra_flor_al_resto");

        Assert.Equal(15, ronda.PuntosFlorActuales); // 15 - max(0, 0)
        Assert.Equal(EstadoRonda.RespondiendoContraFlor, ronda.Estado);
        Assert.Equal(Jugador1, ronda.TurnoActual);
    }

    [Fact]
    public void ResponderFlor_FueraDeTurno_TiraExcepcion()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.CantarFlor(Jugador1);

        var excepcion = Record.Exception(() => ronda.ResponderFlor(Jugador1, "la_mia_es_flor"));

        Assert.IsType<InvalidOperationException>(excepcion);
    }

    [Fact]
    public void ResponderFlor_SinFlorPendiente_TiraExcepcion()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        var excepcion = Record.Exception(() => ronda.ResponderFlor(Jugador1, "la_mia_es_flor"));

        Assert.IsType<InvalidOperationException>(excepcion);
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests --filter "FullyQualifiedName~ResponderFlor"`
Expected: FAIL — error de compilación (`Ronda` no tiene `ResponderFlor`).

- [ ] **Step 3: Implementar `ResponderFlor`**

En `src/TrucoUruguayo.Core/Juego/Ronda.cs`, agregar este método justo después de `CantarFlor`:

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

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests --filter "FullyQualifiedName~ResponderFlor"`
Expected: PASS (5 tests).

- [ ] **Step 5: Correr toda la suite**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests`
Expected: PASS (toda la suite).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Core/Juego/Ronda.cs tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs
git commit -m "$(cat <<'EOF'
feat: agrega Ronda.ResponderFlor (la mia es flor / con flor envido / contra flor al resto)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: `Ronda.ResponderContraFlor`

**Files:**
- Modify: `src/TrucoUruguayo.Core/Juego/Ronda.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`

**Interfaces:**
- Consumes (de Task 1 y 2): `EstadoRonda.RespondiendoContraFlor`, `PuntosFlorActuales`, `_puntosFlorAntesDelUltimoAumento`, `Ronda.ResponderFlor(ulong, string)`.
- Produces (usado por Task 4): `Ronda.ResponderContraFlor(ulong jugadorId, bool quiere)` (`void`, tira `InvalidOperationException` si no corresponde).

- [ ] **Step 1: Escribir los tests que fallan**

Agregar en `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`, después de `ResponderFlor_SinFlorPendiente_TiraExcepcion`:

```csharp
    [Fact]
    public void ResponderContraFlor_QuieroDespuesDeConFlorEnvido_GanaElDeMasPuntosLosOchoPuntos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.CantarFlor(Jugador1);
        ronda.ResponderFlor(Jugador2, "con_flor_envido");

        ronda.ResponderContraFlor(Jugador1, quiere: true);

        Assert.Equal(8, ronda.PuntosJugador1);
        Assert.Equal(0, ronda.PuntosJugador2);
        Assert.Equal(EstadoRonda.JugandoCartas, ronda.Estado);
        Assert.Equal(Jugador1, ronda.TurnoActual);
    }

    [Fact]
    public void ResponderContraFlor_NoQuieroDespuesDeConFlorEnvido_ElQuePropusoGanaSeisPuntos()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.CantarFlor(Jugador1);
        ronda.ResponderFlor(Jugador2, "con_flor_envido");

        ronda.ResponderContraFlor(Jugador1, quiere: false);

        Assert.Equal(0, ronda.PuntosJugador1);
        Assert.Equal(6, ronda.PuntosJugador2);
        Assert.Equal(EstadoRonda.JugandoCartas, ronda.Estado);
    }

    [Fact]
    public void ResponderContraFlor_NoQuieroDespuesDeContraFlorAlResto_ElQuePropusoGanaSeisPuntosNoElResto()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) },
            puntosObjetivo: 15);
        ronda.CantarFlor(Jugador1);
        ronda.ResponderFlor(Jugador2, "contra_flor_al_resto");

        ronda.ResponderContraFlor(Jugador1, quiere: false);

        Assert.Equal(0, ronda.PuntosJugador1);
        Assert.Equal(6, ronda.PuntosJugador2);
    }

    [Fact]
    public void ResponderContraFlor_QuieroDespuesDeContraFlorAlResto_GanaElDeMasPuntosYTerminaLaPartida()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) },
            puntosObjetivo: 15);
        ronda.CantarFlor(Jugador1);
        ronda.ResponderFlor(Jugador2, "contra_flor_al_resto");

        ronda.ResponderContraFlor(Jugador1, quiere: true);

        Assert.Equal(15, ronda.PuntosJugador1);
        Assert.Equal(FaseRonda.Finalizada, ronda.Fase);
        Assert.Equal(Jugador1, ronda.GanadorRonda);
    }

    [Fact]
    public void ResponderContraFlor_FueraDeTurno_TiraExcepcion()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(2, Palo.Oro), new Carta(4, Palo.Oro), new Carta(5, Palo.Oro) },
            new[] { new Carta(11, Palo.Oro), new Carta(10, Palo.Oro), new Carta(3, Palo.Espada) });
        ronda.CantarFlor(Jugador1);
        ronda.ResponderFlor(Jugador2, "con_flor_envido");

        var excepcion = Record.Exception(() => ronda.ResponderContraFlor(Jugador2, true));

        Assert.IsType<InvalidOperationException>(excepcion);
    }

    [Fact]
    public void ResponderContraFlor_SinContraFlorPendiente_TiraExcepcion()
    {
        var ronda = NuevaRondaConMazoFijo(
            new[] { new Carta(1, Palo.Espada), new Carta(3, Palo.Basto), new Carta(6, Palo.Copa) },
            new[] { new Carta(7, Palo.Oro), new Carta(6, Palo.Oro), new Carta(3, Palo.Espada) });

        var excepcion = Record.Exception(() => ronda.ResponderContraFlor(Jugador1, true));

        Assert.IsType<InvalidOperationException>(excepcion);
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests --filter "FullyQualifiedName~ResponderContraFlor"`
Expected: FAIL — error de compilación (`Ronda` no tiene `ResponderContraFlor`).

- [ ] **Step 3: Implementar `ResponderContraFlor`**

En `src/TrucoUruguayo.Core/Juego/Ronda.cs`, agregar este método justo después de `ResponderFlor`:

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

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests --filter "FullyQualifiedName~ResponderContraFlor"`
Expected: PASS (6 tests).

- [ ] **Step 5: Correr toda la suite**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests`
Expected: PASS (toda la suite).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Core/Juego/Ronda.cs tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs
git commit -m "$(cat <<'EOF'
feat: agrega Ronda.ResponderContraFlor

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: UI y handlers en `TrucoModule.cs`

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`

**Interfaces:**
- Consumes (de Task 1, 2, 3): `Ronda.CantarFlor(ulong)`, `Ronda.ResponderFlor(ulong, string)`, `Ronda.ResponderContraFlor(ulong, bool)`, `EstadoRonda.RespondiendoFlor`, `EstadoRonda.RespondiendoContraFlor`.
- Produces: nada nuevo consumido por otras tasks.

Sin tests automatizados (handlers de Discord, mismo criterio que el resto de `TrucoModule`). Se verifica con `dotnet build` + suite completa + prueba manual.

- [ ] **Step 1: Agregar las ramas de `RespondiendoFlor`/`RespondiendoContraFlor` a `ConstruirBotonesDeAccion`**

En `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`, reemplazar:

```csharp
    private static MessageComponent ConstruirBotonesDeAccion(Ronda ronda)
    {
        var botones = new ComponentBuilder()
            .WithButton("🃏 Ver mis cartas", "ver_mano", ButtonStyle.Primary, row: 0);

        if (ronda.Estado == EstadoRonda.EsperandoEnvido || ronda.Estado == EstadoRonda.JugandoCartas)
        {
```

por:

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
```

(el resto del cuerpo de ese bloque — el `if (ronda.PuedeCantarEnvido(...))`, el botón de Truco, el de Irse al Mazo — queda exactamente igual, solo cambió de qué `if` cuelga.)

- [ ] **Step 2: Reescribir la rama `"flor"` de `SeleccionarEnvido`**

Reemplazar:

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

- [ ] **Step 3: Agregar el handler `RespuestaFlor`**

Insertar este método nuevo justo después del cierre de `SeleccionarEnvido` (antes de `[ComponentInteraction("gritar_truco")]`):

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

- [ ] **Step 4: Agregar el handler `ResponderContraFlor`**

Insertar este método nuevo justo después de `RespuestaFlor`:

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

- [ ] **Step 5: Compilar y correr toda la suite**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && dotnet test tests/TrucoUruguayo.Core.Tests && dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: build sin errores, toda la suite en verde.

- [ ] **Step 6: Prueba manual**

Con el bot corriendo, jugar hasta que a ambos jugadores les toque Flor en la misma mano: cantar Flor debe mostrar el select menu "🌸 Responder a la Flor...". Probar cada opción:
- "La mía es Flor" → anuncia el ganador y 6 puntos, pasa a jugar cartas.
- "Con Flor Envido" → muestra "retrucó la Flor", aparecen los botones Quiero/No Quiero; "Quiero" reparte 8 puntos al ganador, "No Quiero" le da 6 a quien propuso.
- "Contra Flor al Resto" → mismo flujo pero el "Quiero" reparte el resto de puntos para el objetivo (puede terminar la partida ahí mismo).

También probar el caso sin cruce (solo un jugador con Flor): debe anunciar "El Envido se anula" y, en el resto de esa mano, el select menu de Envido ya no debe aparecer.

- [ ] **Step 7: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs
git commit -m "$(cat <<'EOF'
feat: agrega el cruce de Flores a los handlers de Discord

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
