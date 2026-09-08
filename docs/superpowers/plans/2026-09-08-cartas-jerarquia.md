# Cartas y GestorDeJerarquia Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar las clases base de cartas (`Palo`, `Pieza`, `Carta`) y el motor `GestorDeJerarquia` que calcula valores de Truco y Envido según la muestra, para el bot de Truco Uruguayo.

**Architecture:** Una librería de clases .NET (`TrucoUruguayo.Core`) sin dependencias externas más allá de la base class library. `GestorDeJerarquia` recibe la carta de muestra en el constructor, y expone consultas de solo lectura (`EsPieza`, `ObtenerPieza`, `ValorEnvido`, `ValorTruco`, `Comparar`, `MejorEnvido`) — no muta estado ni conoce turnos, mazo o partida.

**Tech Stack:** .NET SDK 10 (`dotnet` CLI instalado, versión 10.0.302), C#, xUnit para tests.

## Global Constraints

- Todos los identificadores (clases, métodos, namespaces) en español, consistente con `Palo`, `Pieza`, `Carta` y `GestorDeJerarquia` ya definidos por el usuario en el spec.
- Sin dependencias externas de terceros; solo el SDK base de .NET y xUnit para tests.
- `GestorDeJerarquia` NO modela mazo, descarte de la muestra, turnos ni la ventana en la que se puede cantar Envido — eso es responsabilidad de una clase futura, fuera de este plan.
- `ValorTruco` es puramente ordinal (para comparar cartas), no representa puntos reales del juego.
- Reglas exactas ya confirmadas en `docs/superpowers/specs/2026-09-08-cartas-jerarquia-design.md`: valores de Envido (piezas 30/29/28/27/27, negras 0, comunes = su número), jerarquía de Truco (piezas Dos>Cuatro>Cinco>Caballo>Sota por encima de la jerarquía clásica 1Esp>1Bas>7Esp>7Oro>3>2>1Copa=1Oro>12>11>10>7Copa=7Bas>6>5>4), promoción del 12 cuando la muestra es una pieza (hereda Truco y Envido), y `MejorEnvido` con pieza = valor de la pieza más alta + la carta más alta de las otras dos (sin +20); sin pieza = regla clásica de par de mismo palo +20, o la carta suelta más alta si no hay par.

---

### Task 1: Scaffold del proyecto + Enums Palo/Pieza + clase Carta

**Files:**
- Create: `TrucoUruguayo.sln`
- Create: `src/TrucoUruguayo.Core/TrucoUruguayo.Core.csproj`
- Create: `src/TrucoUruguayo.Core/Modelo/Palo.cs`
- Create: `src/TrucoUruguayo.Core/Modelo/Pieza.cs`
- Create: `src/TrucoUruguayo.Core/Modelo/Carta.cs`
- Create: `tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj`
- Test: `tests/TrucoUruguayo.Core.Tests/Modelo/CartaTests.cs`

**Interfaces:**
- Consumes: nada (primer task)
- Produces:
  - `enum TrucoUruguayo.Core.Modelo.Palo { Espada, Basto, Oro, Copa }`
  - `enum TrucoUruguayo.Core.Modelo.Pieza { Dos, Cuatro, Cinco, Caballo, Sota }`
  - `class TrucoUruguayo.Core.Modelo.Carta` con `int Numero { get; }`, `Palo Palo { get; }`, constructor `Carta(int numero, Palo palo)` que valida el número y arroja `ArgumentOutOfRangeException` si es inválido, y `Equals`/`GetHashCode`/`ToString` por valor.

- [ ] **Step 1: Scaffold de la solución y los proyectos**

Ejecutar desde la raíz del repo (`Truco-Uruguayo/`):

```bash
dotnet new sln -n TrucoUruguayo
dotnet new classlib -n TrucoUruguayo.Core -o src/TrucoUruguayo.Core
dotnet new xunit -n TrucoUruguayo.Core.Tests -o tests/TrucoUruguayo.Core.Tests
dotnet sln add src/TrucoUruguayo.Core/TrucoUruguayo.Core.csproj
dotnet sln add tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj
dotnet add tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj reference src/TrucoUruguayo.Core/TrucoUruguayo.Core.csproj
rm src/TrucoUruguayo.Core/Class1.cs
rm tests/TrucoUruguayo.Core.Tests/UnitTest1.cs
mkdir -p src/TrucoUruguayo.Core/Modelo src/TrucoUruguayo.Core/Jerarquia
mkdir -p tests/TrucoUruguayo.Core.Tests/Modelo tests/TrucoUruguayo.Core.Tests/Jerarquia
```

- [ ] **Step 2: Escribir los enums**

`src/TrucoUruguayo.Core/Modelo/Palo.cs`:

```csharp
namespace TrucoUruguayo.Core.Modelo;

public enum Palo
{
    Espada,
    Basto,
    Oro,
    Copa,
}
```

`src/TrucoUruguayo.Core/Modelo/Pieza.cs`:

```csharp
namespace TrucoUruguayo.Core.Modelo;

public enum Pieza
{
    Dos,
    Cuatro,
    Cinco,
    Caballo,
    Sota,
}
```

- [ ] **Step 3: Escribir el test que falla para Carta**

`tests/TrucoUruguayo.Core.Tests/Modelo/CartaTests.cs`:

```csharp
using System;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Modelo;

public class CartaTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(12)]
    public void Constructor_AceptaNumerosValidos(int numero)
    {
        var carta = new Carta(numero, Palo.Oro);

        Assert.Equal(numero, carta.Numero);
        Assert.Equal(Palo.Oro, carta.Palo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(13)]
    public void Constructor_RechazaNumerosInvalidos(int numero)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Carta(numero, Palo.Oro));
    }

    [Fact]
    public void Equals_ComparaPorValor()
    {
        var a = new Carta(4, Palo.Espada);
        var b = new Carta(4, Palo.Espada);
        var c = new Carta(4, Palo.Basto);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
```

- [ ] **Step 4: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~CartaTests"`
Expected: la build FALLA — `Carta` no existe todavía (error CS0246).

- [ ] **Step 5: Implementar Carta**

`src/TrucoUruguayo.Core/Modelo/Carta.cs`:

```csharp
using System;
using System.Linq;

namespace TrucoUruguayo.Core.Modelo;

public class Carta
{
    private static readonly int[] NumerosValidos = { 1, 2, 3, 4, 5, 6, 7, 10, 11, 12 };

    public int Numero { get; }
    public Palo Palo { get; }

    public Carta(int numero, Palo palo)
    {
        if (!NumerosValidos.Contains(numero))
        {
            throw new ArgumentOutOfRangeException(
                nameof(numero), numero, "El numero debe estar entre 1 y 12, salteando 8 y 9.");
        }

        Numero = numero;
        Palo = palo;
    }

    public override bool Equals(object? obj)
    {
        return obj is Carta otra && Numero == otra.Numero && Palo == otra.Palo;
    }

    public override int GetHashCode() => HashCode.Combine(Numero, Palo);

    public override string ToString() => $"{Numero} de {Palo}";
}
```

- [ ] **Step 6: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~CartaTests"`
Expected: PASS (9 tests: 4 + 4 + 1).

- [ ] **Step 7: Commit**

```bash
git add TrucoUruguayo.sln src/TrucoUruguayo.Core tests/TrucoUruguayo.Core.Tests
git commit -m "Agrega enums Palo/Pieza y clase Carta con validacion"
```

---

### Task 2: Identificación de piezas y promoción del 12

**Files:**
- Create: `src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs`

**Interfaces:**
- Consumes: `Carta`, `Palo`, `Pieza` (Task 1)
- Produces:
  - `class TrucoUruguayo.Core.Jerarquia.GestorDeJerarquia` con constructor `GestorDeJerarquia(Carta muestra)`
  - `bool EsPieza(Carta carta)`
  - `Pieza? ObtenerPieza(Carta carta)`

- [ ] **Step 1: Escribir los tests que fallan**

`tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs`:

```csharp
using TrucoUruguayo.Core.Jerarquia;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Core.Tests.Jerarquia;

public class GestorDeJerarquiaTests
{
    [Fact]
    public void EsPieza_CartaDelPaloYNumeroDeMuestra_EsPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var dosDeOro = new Carta(2, Palo.Oro);

        Assert.True(gestor.EsPieza(dosDeOro));
        Assert.Equal(Pieza.Dos, gestor.ObtenerPieza(dosDeOro));
    }

    [Fact]
    public void EsPieza_MismoNumeroOtroPalo_NoEsPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var dosDeEspada = new Carta(2, Palo.Espada);

        Assert.False(gestor.EsPieza(dosDeEspada));
        Assert.Null(gestor.ObtenerPieza(dosDeEspada));
    }

    [Fact]
    public void EsPieza_NumeroFueraDeLasCincoPiezas_NoEsPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var sieteDeOro = new Carta(7, Palo.Oro);

        Assert.False(gestor.EsPieza(sieteDeOro));
    }

    [Fact]
    public void ObtenerPieza_MuestraEsPieza_El12DelPaloReemplazaLaPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(4, Palo.Oro)); // la muestra misma es la pieza "Cuatro"

        var doceDeOro = new Carta(12, Palo.Oro);

        Assert.True(gestor.EsPieza(doceDeOro));
        Assert.Equal(Pieza.Cuatro, gestor.ObtenerPieza(doceDeOro));
    }

    [Fact]
    public void ObtenerPieza_MuestraNoEsPieza_El12DelPaloNoEsPieza()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro)); // 3 no es rango de pieza

        var doceDeOro = new Carta(12, Palo.Oro);

        Assert.False(gestor.EsPieza(doceDeOro));
    }
}
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~GestorDeJerarquiaTests"`
Expected: la build FALLA — `GestorDeJerarquia` no existe todavía (error CS0246).

- [ ] **Step 3: Implementar GestorDeJerarquia (constructor, EsPieza, ObtenerPieza)**

`src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs`:

```csharp
using System;
using System.Collections.Generic;
using TrucoUruguayo.Core.Modelo;

namespace TrucoUruguayo.Core.Jerarquia;

public class GestorDeJerarquia
{
    private static readonly Dictionary<int, Pieza> NumeroAPieza = new()
    {
        [2] = Pieza.Dos,
        [4] = Pieza.Cuatro,
        [5] = Pieza.Cinco,
        [11] = Pieza.Caballo,
        [10] = Pieza.Sota,
    };

    private readonly Carta _muestra;
    private readonly Pieza? _piezaPromovida;

    public GestorDeJerarquia(Carta muestra)
    {
        _muestra = muestra ?? throw new ArgumentNullException(nameof(muestra));
        _piezaPromovida = NumeroAPieza.TryGetValue(muestra.Numero, out var pieza) ? pieza : (Pieza?)null;
    }

    public bool EsPieza(Carta carta) => ObtenerPieza(carta) != null;

    public Pieza? ObtenerPieza(Carta carta)
    {
        if (carta.Palo != _muestra.Palo)
        {
            return null;
        }

        if (carta.Numero == 12 && _piezaPromovida != null)
        {
            return _piezaPromovida;
        }

        return NumeroAPieza.TryGetValue(carta.Numero, out var pieza) ? pieza : (Pieza?)null;
    }
}
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~GestorDeJerarquiaTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TrucoUruguayo.Core/Jerarquia tests/TrucoUruguayo.Core.Tests/Jerarquia
git commit -m "Agrega identificacion de piezas y promocion del 12 en GestorDeJerarquia"
```

---

### Task 3: ValorEnvido por carta

**Files:**
- Modify: `src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs`

**Interfaces:**
- Consumes: `ObtenerPieza(Carta)` (Task 2)
- Produces: `int ValorEnvido(Carta carta)`

- [ ] **Step 1: Agregar los tests que fallan**

Agregar a `tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs` (dentro de la clase `GestorDeJerarquiaTests`):

```csharp
    [Theory]
    [InlineData(2, 30)]
    [InlineData(4, 29)]
    [InlineData(5, 28)]
    [InlineData(11, 27)]
    [InlineData(10, 27)]
    public void ValorEnvido_Piezas_DevuelveValorFijo(int numero, int envidoEsperado)
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var pieza = new Carta(numero, Palo.Oro);

        Assert.Equal(envidoEsperado, gestor.ValorEnvido(pieza));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void ValorEnvido_FigurasNegras_ValenCero(int numero)
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var negra = new Carta(numero, Palo.Espada); // distinto palo que la muestra

        Assert.Equal(0, gestor.ValorEnvido(negra));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    public void ValorEnvido_CartasComunes_ValenSuNumero(int numero)
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var comun = new Carta(numero, Palo.Copa);

        Assert.Equal(numero, gestor.ValorEnvido(comun));
    }

    [Fact]
    public void ValorEnvido_DocePromovido_HeredaElValorDeLaPiezaQueReemplaza()
    {
        var gestor = new GestorDeJerarquia(new Carta(4, Palo.Oro)); // pieza Cuatro

        var doceDeOro = new Carta(12, Palo.Oro);

        Assert.Equal(29, gestor.ValorEnvido(doceDeOro));
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~ValorEnvido"`
Expected: la build FALLA — `ValorEnvido` no existe todavía (error CS1061).

- [ ] **Step 3: Implementar ValorEnvido**

Agregar a la clase `GestorDeJerarquia` en `src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs`:

```csharp
    private static readonly Dictionary<Pieza, int> ValorEnvidoPorPieza = new()
    {
        [Pieza.Dos] = 30,
        [Pieza.Cuatro] = 29,
        [Pieza.Cinco] = 28,
        [Pieza.Caballo] = 27,
        [Pieza.Sota] = 27,
    };

    public int ValorEnvido(Carta carta)
    {
        var pieza = ObtenerPieza(carta);
        if (pieza != null)
        {
            return ValorEnvidoPorPieza[pieza.Value];
        }

        if (carta.Numero is 10 or 11 or 12)
        {
            return 0;
        }

        return carta.Numero;
    }
```

(Agregar el diccionario `ValorEnvidoPorPieza` junto a `NumeroAPieza`, y el método `ValorEnvido` como miembro público de la clase.)

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~ValorEnvido"`
Expected: PASS (13 tests: 5 + 3 + 4 + 1).

- [ ] **Step 5: Commit**

```bash
git add src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs
git commit -m "Agrega ValorEnvido por carta en GestorDeJerarquia"
```

---

### Task 4: ValorTruco y Comparar

**Files:**
- Modify: `src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs`

**Interfaces:**
- Consumes: `ObtenerPieza(Carta)` (Task 2)
- Produces: `int ValorTruco(Carta carta)`, `int Comparar(Carta a, Carta b)`

- [ ] **Step 1: Agregar los tests que fallan**

Agregar a `tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs`:

```csharp
    [Fact]
    public void ValorTruco_OrdenDeLasPiezas()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var dos = new Carta(2, Palo.Oro);
        var cuatro = new Carta(4, Palo.Oro);
        var cinco = new Carta(5, Palo.Oro);
        var caballo = new Carta(11, Palo.Oro);
        var sota = new Carta(10, Palo.Oro);

        Assert.True(gestor.ValorTruco(dos) > gestor.ValorTruco(cuatro));
        Assert.True(gestor.ValorTruco(cuatro) > gestor.ValorTruco(cinco));
        Assert.True(gestor.ValorTruco(cinco) > gestor.ValorTruco(caballo));
        Assert.True(gestor.ValorTruco(caballo) > gestor.ValorTruco(sota));
    }

    [Fact]
    public void ValorTruco_PiezaSiempreLeGanaACualquierCartaComun()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var sota = new Carta(10, Palo.Oro); // pieza mas baja
        var ancho = new Carta(1, Palo.Espada); // carta comun mas alta

        Assert.True(gestor.ValorTruco(sota) > gestor.ValorTruco(ancho));
    }

    [Fact]
    public void ValorTruco_JerarquiaClasica()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        var anchoEspada = new Carta(1, Palo.Espada);
        var anchoBasto = new Carta(1, Palo.Basto);
        var sieteEspada = new Carta(7, Palo.Espada);
        var sieteOro = new Carta(7, Palo.Oro);
        var tres = new Carta(3, Palo.Copa);
        var dos = new Carta(2, Palo.Oro); // no Palo.Copa: 2 del palo de la muestra seria pieza
        var anchoFalso = new Carta(1, Palo.Copa);
        var doce = new Carta(12, Palo.Copa);
        var once = new Carta(11, Palo.Oro); // no Palo.Copa: 11 del palo de la muestra seria pieza
        var diez = new Carta(10, Palo.Oro); // no Palo.Copa: 10 del palo de la muestra seria pieza
        var sieteBasto = new Carta(7, Palo.Basto);
        var seisComun = new Carta(6, Palo.Basto);
        var cinco = new Carta(5, Palo.Oro); // no Palo.Copa: 5 del palo de la muestra seria pieza
        var cuatro = new Carta(4, Palo.Oro); // no Palo.Copa: 4 del palo de la muestra seria pieza

        Assert.True(gestor.ValorTruco(anchoEspada) > gestor.ValorTruco(anchoBasto));
        Assert.True(gestor.ValorTruco(anchoBasto) > gestor.ValorTruco(sieteEspada));
        Assert.True(gestor.ValorTruco(sieteEspada) > gestor.ValorTruco(sieteOro));
        Assert.True(gestor.ValorTruco(sieteOro) > gestor.ValorTruco(tres));
        Assert.True(gestor.ValorTruco(tres) > gestor.ValorTruco(dos));
        Assert.True(gestor.ValorTruco(dos) > gestor.ValorTruco(anchoFalso));
        Assert.True(gestor.ValorTruco(anchoFalso) > gestor.ValorTruco(doce));
        Assert.True(gestor.ValorTruco(doce) > gestor.ValorTruco(once));
        Assert.True(gestor.ValorTruco(once) > gestor.ValorTruco(diez));
        Assert.True(gestor.ValorTruco(diez) > gestor.ValorTruco(sieteBasto));
        Assert.True(gestor.ValorTruco(sieteBasto) > gestor.ValorTruco(seisComun));
        Assert.True(gestor.ValorTruco(seisComun) > gestor.ValorTruco(cinco));
        Assert.True(gestor.ValorTruco(cinco) > gestor.ValorTruco(cuatro));
    }

    [Fact]
    public void ValorTruco_EmpatesClasicos()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        Assert.Equal(gestor.ValorTruco(new Carta(1, Palo.Copa)), gestor.ValorTruco(new Carta(1, Palo.Oro)));
        Assert.Equal(gestor.ValorTruco(new Carta(7, Palo.Copa)), gestor.ValorTruco(new Carta(7, Palo.Basto)));
    }

    [Fact]
    public void ValorTruco_DocePromovido_OcupaElRangoDeLaPiezaQueReemplaza()
    {
        var gestor = new GestorDeJerarquia(new Carta(4, Palo.Oro)); // pieza Cuatro

        var doceDeOro = new Carta(12, Palo.Oro);
        var dosDeOro = new Carta(2, Palo.Oro);   // pieza Dos, sigue existiendo normal
        var cincoDeOro = new Carta(5, Palo.Oro); // pieza Cinco, sigue existiendo normal

        Assert.True(gestor.ValorTruco(dosDeOro) > gestor.ValorTruco(doceDeOro));
        Assert.True(gestor.ValorTruco(doceDeOro) > gestor.ValorTruco(cincoDeOro));
    }

    [Fact]
    public void Comparar_DevuelvePositivoNegativoOCero()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        var fuerte = new Carta(1, Palo.Espada);
        var debil = new Carta(4, Palo.Oro); // no Palo.Copa: 4 del palo de la muestra seria pieza
        var empateA = new Carta(1, Palo.Copa);
        var empateB = new Carta(1, Palo.Oro);

        Assert.True(gestor.Comparar(fuerte, debil) > 0);
        Assert.True(gestor.Comparar(debil, fuerte) < 0);
        Assert.Equal(0, gestor.Comparar(empateA, empateB));
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~ValorTruco|FullyQualifiedName~Comparar"`
Expected: la build FALLA — `ValorTruco` y `Comparar` no existen todavía (error CS1061).

- [ ] **Step 3: Implementar ValorTruco y Comparar**

Agregar a la clase `GestorDeJerarquia`:

```csharp
    private static readonly Dictionary<Pieza, int> ValorTrucoPorPieza = new()
    {
        [Pieza.Dos] = 19,
        [Pieza.Cuatro] = 18,
        [Pieza.Cinco] = 17,
        [Pieza.Caballo] = 16,
        [Pieza.Sota] = 15,
    };

    public int ValorTruco(Carta carta)
    {
        var pieza = ObtenerPieza(carta);
        if (pieza != null)
        {
            return ValorTrucoPorPieza[pieza.Value];
        }

        return (carta.Numero, carta.Palo) switch
        {
            (1, Palo.Espada) => 14,
            (1, Palo.Basto) => 13,
            (7, Palo.Espada) => 12,
            (7, Palo.Oro) => 11,
            (3, _) => 10,
            (2, _) => 9,
            (1, _) => 8,
            (12, _) => 7,
            (11, _) => 6,
            (10, _) => 5,
            (7, _) => 4,
            (6, _) => 3,
            (5, _) => 2,
            (4, _) => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(carta), carta, "Numero de carta invalido."),
        };
    }

    public int Comparar(Carta a, Carta b) => ValorTruco(a).CompareTo(ValorTruco(b));
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~ValorTruco|FullyQualifiedName~Comparar"`
Expected: PASS (6 tests).

- [ ] **Step 5: Correr toda la suite antes de commitear**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj`
Expected: PASS (33 tests en total).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs
git commit -m "Agrega ValorTruco y Comparar en GestorDeJerarquia"
```

---

### Task 5: MejorEnvido de una mano de 3 cartas

**Files:**
- Modify: `src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs`
- Test: `tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs`

**Interfaces:**
- Consumes: `EsPieza(Carta)` (Task 2), `ValorEnvido(Carta)` (Task 3)
- Produces: `int MejorEnvido(Carta[] mano)`

- [ ] **Step 1: Agregar los tests que fallan**

Agregar a `tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs`:

```csharp
    [Fact]
    public void MejorEnvido_SinPieza_MismoPaloSumaMas20()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        var mano = new[]
        {
            new Carta(7, Palo.Espada),
            new Carta(6, Palo.Espada),
            new Carta(3, Palo.Oro),
        };

        Assert.Equal(33, gestor.MejorEnvido(mano)); // 7 + 6 + 20, mismo palo Espada
    }

    [Fact]
    public void MejorEnvido_SinPieza_SinParTomaLaCartaMasAlta()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        var mano = new[]
        {
            new Carta(7, Palo.Espada),
            new Carta(3, Palo.Oro),
            new Carta(1, Palo.Basto),
        };

        Assert.Equal(7, gestor.MejorEnvido(mano));
    }

    [Fact]
    public void MejorEnvido_ConPieza_SumaLaCartaMasAltaSinBonificacion()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var mano = new[]
        {
            new Carta(2, Palo.Oro),    // pieza Dos, envido 30
            new Carta(7, Palo.Oro),    // mismo palo que la pieza, pero NO se suma +20
            new Carta(6, Palo.Espada),
        };

        Assert.Equal(37, gestor.MejorEnvido(mano)); // 30 + 7
    }

    [Fact]
    public void MejorEnvido_ConDosPiezas_UsaLaMasAltaComoBaseYLaOtraComoSuma()
    {
        var gestor = new GestorDeJerarquia(new Carta(3, Palo.Oro));

        var mano = new[]
        {
            new Carta(2, Palo.Oro),    // pieza Dos, envido 30
            new Carta(4, Palo.Oro),    // pieza Cuatro, envido 29
            new Carta(6, Palo.Espada), // envido 6
        };

        Assert.Equal(59, gestor.MejorEnvido(mano)); // 30 + 29
    }

    [Fact]
    public void MejorEnvido_ManoConDistintoDeTresCartas_Arroja()
    {
        var gestor = new GestorDeJerarquia(new Carta(6, Palo.Copa));

        var manoDeDos = new[] { new Carta(1, Palo.Espada), new Carta(2, Palo.Oro) };

        Assert.Throws<ArgumentException>(() => gestor.MejorEnvido(manoDeDos));
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~MejorEnvido"`
Expected: la build FALLA — `MejorEnvido` no existe todavía (error CS1061).

- [ ] **Step 3: Implementar MejorEnvido**

Agregar a la clase `GestorDeJerarquia` (requiere `using System.Linq;` al inicio del archivo):

```csharp
    public int MejorEnvido(Carta[] mano)
    {
        if (mano == null || mano.Length != 3)
        {
            throw new ArgumentException("La mano debe tener exactamente 3 cartas.", nameof(mano));
        }

        var piezasEnMano = mano.Where(EsPieza).ToList();
        if (piezasEnMano.Count > 0)
        {
            var basePieza = piezasEnMano.OrderByDescending(ValorEnvido).First();
            var restantes = new List<Carta>(mano);
            restantes.Remove(basePieza);
            var mejorRestante = restantes.Max(ValorEnvido);
            return ValorEnvido(basePieza) + mejorRestante;
        }

        int? mejorPar = null;
        for (var i = 0; i < mano.Length; i++)
        {
            for (var j = i + 1; j < mano.Length; j++)
            {
                if (mano[i].Palo != mano[j].Palo)
                {
                    continue;
                }

                var suma = ValorEnvido(mano[i]) + ValorEnvido(mano[j]) + 20;
                if (mejorPar == null || suma > mejorPar)
                {
                    mejorPar = suma;
                }
            }
        }

        return mejorPar ?? mano.Max(ValorEnvido);
    }
```

Agregar `using System.Linq;` junto a los demás `using` al principio de `src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs`.

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj --filter "FullyQualifiedName~MejorEnvido"`
Expected: PASS (5 tests).

- [ ] **Step 5: Correr toda la suite antes de commitear**

Run: `dotnet test tests/TrucoUruguayo.Core.Tests/TrucoUruguayo.Core.Tests.csproj`
Expected: PASS (38 tests en total).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Core/Jerarquia/GestorDeJerarquia.cs tests/TrucoUruguayo.Core.Tests/Jerarquia/GestorDeJerarquiaTests.cs
git commit -m "Agrega MejorEnvido de mano en GestorDeJerarquia"
```
