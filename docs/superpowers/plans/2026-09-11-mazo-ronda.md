# Mazo y Ronda Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar el mazo de cartas (`Mazo`, `Reparto`) y la estructura base de una ronda de juego (`Ronda`, `EstadoRonda`) para el bot de Truco Uruguayo, con reparto correcto que aísla la muestra.

**Architecture:** Nuevo namespace `TrucoUruguayo.Core.Juego`, en paralelo a los ya existentes `Modelo` y `Jerarquia`. `Mazo` es autocontenido (arma y baraja sus propias 40 cartas). `Ronda` orquesta un `Mazo` y un `GestorDeJerarquia` (ya existente) para armar el estado inicial de una mano de juego — sin lógica de jugar cartas ni cantar todavía.

**Tech Stack:** .NET SDK 10, C#, xUnit. Proyecto `TrucoUruguayo.Core` y `TrucoUruguayo.Core.Tests` ya existen y están configurados (ver `src/TrucoUruguayo.Core/Modelo` y `Jerarquia` para el estilo ya establecido).

## Global Constraints

- Todos los identificadores (clases, métodos, namespaces) en español.
- Namespace nuevo: `TrucoUruguayo.Core.Juego` (archivos en `src/TrucoUruguayo.Core/Juego/`, tests en `tests/TrucoUruguayo.Core.Tests/Juego/`).
- Sin dependencias externas de terceros.
- `Mazo` tiene exactamente 3 miembros públicos: constructor sin parámetros, `Mezclar()`, `Repartir(): Reparto`. Nada más (no exponer la lista interna de cartas).
- `Mezclar()` NO baraja "lo que queda" del mazo: cada llamada regenera el mazo completo de 40 cartas desde cero (4 palos x {1,2,3,4,5,6,7,10,11,12}) y recién ahí lo baraja (Fisher-Yates). `Repartir()` no vuelve a barajar ni valida que `Mezclar()` se haya llamado antes; simplemente reparte del estado actual.
- `Repartir()` saca la Muestra primero (queda aislada del mazo antes de repartir las manos) y después 3 cartas para cada jugador.
- `Reparto` es una clase record: `public sealed record Reparto(Carta Muestra, List<Carta> ManoJugador1, List<Carta> ManoJugador2);`.
- `Ronda`: IDs de jugador tipo `ulong`. Constructor `Ronda(ulong jugador1Id, ulong jugador2Id)` crea su propio `Mazo`, lo mezcla, reparte, y arma un `GestorDeJerarquia` con la Muestra resultante. `Estado` arranca en `EstadoRonda.EsperandoEnvido`. `TurnoActual` arranca en `jugador1Id`. El `Mazo` interno de la ronda no se expone como propiedad pública.
- Fuera de alcance de este plan (no implementar): jugar una carta, cantar Envido/Truco, lógica real de turnos/rotación, integración con Discord, persistencia en Postgres.

---

### Task 1: Mazo y Reparto

**Files:**
- Create: `src/TrucoUruguayo.Core/Juego/Reparto.cs`
- Create: `src/TrucoUruguayo.Core/Juego/Mazo.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Juego/MazoTests.cs`

**Interfaces:**
- Consumes: `TrucoUruguayo.Core.Modelo.Carta`, `TrucoUruguayo.Core.Modelo.Palo` (ya existentes)
- Produces:
  - `public sealed record Reparto(Carta Muestra, List<Carta> ManoJugador1, List<Carta> ManoJugador2)` en `TrucoUruguayo.Core.Juego`
  - `public class Mazo` en `TrucoUruguayo.Core.Juego`, con `Mazo()`, `void Mezclar()`, `Reparto Repartir()`

- [ ] **Step 1: Crear Reparto**

`src/TrucoUruguayo.Core/Juego/Reparto.cs`:

```csharp
using System.Collections.Generic;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Juego;

public sealed record Reparto(Carta Muestra, List<Carta> ManoJugador1, List<Carta> ManoJugador2);
```

- [ ] **Step 2: Escribir los tests que fallan**

`tests/TrucoUruguayo.Core.Tests/Juego/MazoTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using TrucoUruguayo.Core.Juego;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Juego;

public class MazoTests
{
    [Fact]
    public void Repartir_LaMuestraNuncaEstaEnNingunaMano()
    {
        for (var i = 0; i < 50; i++)
        {
            var mazo = new Mazo();
            mazo.Mezclar();

            var reparto = mazo.Repartir();

            Assert.DoesNotContain(reparto.Muestra, reparto.ManoJugador1);
            Assert.DoesNotContain(reparto.Muestra, reparto.ManoJugador2);
        }
    }

    [Fact]
    public void Repartir_CadaJugadorRecibeExactamenteTresCartas()
    {
        for (var i = 0; i < 50; i++)
        {
            var mazo = new Mazo();
            mazo.Mezclar();

            var reparto = mazo.Repartir();

            Assert.Equal(3, reparto.ManoJugador1.Count);
            Assert.Equal(3, reparto.ManoJugador2.Count);
        }
    }

    [Fact]
    public void Repartir_LasSieteCartasRepartidasSonTodasDistintas()
    {
        for (var i = 0; i < 50; i++)
        {
            var mazo = new Mazo();
            mazo.Mezclar();

            var reparto = mazo.Repartir();

            var todasLasCartas = new List<Carta> { reparto.Muestra };
            todasLasCartas.AddRange(reparto.ManoJugador1);
            todasLasCartas.AddRange(reparto.ManoJugador2);

            Assert.Equal(7, todasLasCartas.Distinct().Count());
        }
    }
}
```

- [ ] **Step 3: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~MazoTests"`
Expected: la build FALLA — `Mazo` no existe todavía (error CS0246).

- [ ] **Step 4: Implementar Mazo**

`src/TrucoUruguayo.Core/Juego/Mazo.cs`:

```csharp
using System;
using System.Collections.Generic;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Juego;

public class Mazo
{
    private static readonly int[] NumerosValidos = { 1, 2, 3, 4, 5, 6, 7, 10, 11, 12 };

    private readonly Random _random;
    private List<Carta> _cartas;

    public Mazo()
    {
        _random = new Random();
        _cartas = GenerarMazoCompleto();
    }

    public void Mezclar()
    {
        _cartas = GenerarMazoCompleto();

        for (var i = _cartas.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_cartas[i], _cartas[j]) = (_cartas[j], _cartas[i]);
        }
    }

    public Reparto Repartir()
    {
        var muestra = _cartas[0];
        var manoJugador1 = _cartas.GetRange(1, 3);
        var manoJugador2 = _cartas.GetRange(4, 3);

        return new Reparto(muestra, manoJugador1, manoJugador2);
    }

    private static List<Carta> GenerarMazoCompleto()
    {
        var cartas = new List<Carta>();

        foreach (Palo palo in Enum.GetValues<Palo>())
        {
            foreach (var numero in NumerosValidos)
            {
                cartas.Add(new Carta(numero, palo));
            }
        }

        return cartas;
    }
}
```

- [ ] **Step 5: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~MazoTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Core/Juego/Reparto.cs src/TrucoUruguayo.Core/Juego/Mazo.cs tests/TrucoUruguayo.Core.Tests/Juego/MazoTests.cs
git commit -m "Agrega Mazo y Reparto"
```

---

### Task 2: EstadoRonda y Ronda

**Files:**
- Create: `src/TrucoUruguayo.Core/Juego/EstadoRonda.cs`
- Create: `src/TrucoUruguayo.Core/Juego/Ronda.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`

**Interfaces:**
- Consumes:
  - `TrucoUruguayo.Core.Juego.Mazo` — `Mazo()`, `void Mezclar()`, `Reparto Repartir()` (Task 1)
  - `TrucoUruguayo.Core.Juego.Reparto` — `Carta Muestra`, `List<Carta> ManoJugador1`, `List<Carta> ManoJugador2` (Task 1)
  - `TrucoUruguayo.Core.Jerarquia.GestorDeJerarquia` — constructor `GestorDeJerarquia(Carta muestra)`, `Pieza? ObtenerPieza(Carta carta)`, `int ValorTruco(Carta carta)` (ya existente)
  - `TrucoUruguayo.Core.Modelo.Carta`, `Pieza` (ya existentes)
- Produces:
  - `public enum EstadoRonda { EsperandoEnvido, JugandoCartas, Finalizada }` en `TrucoUruguayo.Core.Juego`
  - `public class Ronda` en `TrucoUruguayo.Core.Juego`, con `Ronda(ulong jugador1Id, ulong jugador2Id)` y las propiedades `Jugador1Id`, `Jugador2Id`, `Muestra`, `ManoJugador1`, `ManoJugador2`, `Gestor`, `Estado`, `TurnoActual`

- [ ] **Step 1: Crear EstadoRonda**

`src/TrucoUruguayo.Core/Juego/EstadoRonda.cs`:

```csharp
namespace TrucoUruguayo.Core.Juego;

public enum EstadoRonda
{
    EsperandoEnvido,
    JugandoCartas,
    Finalizada,
}
```

- [ ] **Step 2: Escribir los tests que fallan**

`tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs`:

```csharp
using System.Linq;
using TrucoUruguayo.Core.Juego;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Juego;

public class RondaTests
{
    [Fact]
    public void Constructor_NingunJugadorRecibeLaMuestraYAmbosTienenTresCartas()
    {
        for (var i = 0; i < 50; i++)
        {
            var ronda = new Ronda(1UL, 2UL);

            Assert.Equal(3, ronda.ManoJugador1.Count);
            Assert.Equal(3, ronda.ManoJugador2.Count);
            Assert.DoesNotContain(ronda.Muestra, ronda.ManoJugador1);
            Assert.DoesNotContain(ronda.Muestra, ronda.ManoJugador2);
            Assert.Empty(ronda.ManoJugador1.Intersect(ronda.ManoJugador2));
        }
    }

    [Fact]
    public void Constructor_EstadoArrancaEnEsperandoEnvido()
    {
        var ronda = new Ronda(1UL, 2UL);

        Assert.Equal(EstadoRonda.EsperandoEnvido, ronda.Estado);
    }

    [Fact]
    public void Constructor_TurnoActualArrancaEnJugador1()
    {
        var ronda = new Ronda(42UL, 99UL);

        Assert.Equal(42UL, ronda.TurnoActual);
    }

    [Fact]
    public void Constructor_ArmaElGestorConLaMuestraDeEstaRonda()
    {
        for (var i = 0; i < 50; i++)
        {
            var ronda = new Ronda(1UL, 2UL);

            var dosDelPaloDeLaMuestra = new Carta(2, ronda.Muestra.Palo);

            Assert.Equal(Pieza.Dos, ronda.Gestor.ObtenerPieza(dosDelPaloDeLaMuestra));
        }
    }
}
```

- [ ] **Step 3: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~RondaTests"`
Expected: la build FALLA — `Ronda` no existe todavía (error CS0246).

- [ ] **Step 4: Implementar Ronda**

`src/TrucoUruguayo.Core/Juego/Ronda.cs`:

```csharp
using System.Collections.Generic;
using TrucoUruguayo.Core.Jerarquia;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Juego;

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
        Jugador1Id = jugador1Id;
        Jugador2Id = jugador2Id;

        var mazo = new Mazo();
        mazo.Mezclar();
        var reparto = mazo.Repartir();

        Muestra = reparto.Muestra;
        ManoJugador1 = reparto.ManoJugador1;
        ManoJugador2 = reparto.ManoJugador2;
        Gestor = new GestorDeJerarquia(Muestra);

        Estado = EstadoRonda.EsperandoEnvido;
        TurnoActual = jugador1Id;
    }
}
```

- [ ] **Step 5: Correr los tests y verificar que pasan, y correr la suite completa**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~RondaTests"`
Expected: PASS (4 tests).

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj`
Expected: PASS (49 tests en total: 42 existentes + 3 de MazoTests + 4 de RondaTests).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Core/Juego/EstadoRonda.cs src/TrucoUruguayo.Core/Juego/Ronda.cs tests/TrucoUruguayo.Core.Tests/Juego/RondaTests.cs
git commit -m "Agrega EstadoRonda y Ronda"
```
