# Mazos organizados, dorso bajo la muestra y fondo de mesa — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Organizar el mazo actual y uno nuevo ("clásico") en carpetas separadas, y mejorar la imagen de mesa con un dorso asomando detrás de la muestra y un fondo de madera.

**Architecture:** Los 40 archivos del mazo actual se mueven a `assets/cartas/mazo_basico/`; el mazo nuevo (tomado de `github.com/RolandoAndrade/juego-truco`) se copia a `assets/cartas/mazo_clasico/` con la misma convención de nombres; su dorso se copia como archivo compartido `assets/cartas/dorso.png`. `GeneradorImagenes` sigue usando un solo mazo activo (`mazo_basico`), y `GenerarMesaActualAsync` gana dos capas nuevas: un fondo de madera cubriendo todo el lienzo, y una pila de copias del dorso desplazadas detrás de la muestra.

**Tech Stack:** C# / .NET 10, SixLabors.ImageSharp 4.1.2, xUnit.

## Global Constraints

- El bot sigue usando un único mazo activo (`mazo_basico`) — no se agrega ningún mecanismo para elegir mazo, queda fuera de alcance.
- Mapeo de palos del repo externo: `sword`→espada, `cup`→copa, `gold`→oro, `course`→basto (confirmado vía `Serial/SerialManager.java`, que mapea `"course"` a la letra `"B"` de Basto).
- El dorso (`dorso.png`) es compartido entre mazos, no específico de `mazo_clasico` — se usa siempre en la mesa, sin importar el mazo activo.
- Pila de dorso: 2 copias, desplazadas `6px` cada una (hacia abajo y a la derecha) detrás de la muestra — agrega `12px` de ancho y de alto al grupo "muestra + pila".
- El fondo de madera va solo en `GenerarMesaActualAsync` (la imagen de mesa) — `GenerarManoAsync` (la vista privada de la mano) no se toca.
- `assets/muestra_bajocarta.png` (imagen de referencia que mandó el usuario) no se toca.

Spec completo: `docs/superpowers/specs/2026-09-16-mazos-y-mesa.md`

---

### Task 1: Mover el mazo actual a `mazo_basico/`

**Files:**
- Modify (mover): `src/TrucoUruguayo.Bot/assets/cartas/*.png` → `src/TrucoUruguayo.Bot/assets/cartas/mazo_basico/*.png`
- Modify (mover): `src/TrucoUruguayo.Bot/assets/cartas/ATRIBUCION.md` → `src/TrucoUruguayo.Bot/assets/cartas/mazo_basico/ATRIBUCION.md`
- Modify: `src/TrucoUruguayo.Bot/Servicios/GeneradorImagenes.cs`
- Test: `tests/TrucoUruguayo.Bot.Tests/Servicios/GeneradorImagenesTests.cs`

**Interfaces:**
- Consumes: nada nuevo.
- Produces: `GeneradorImagenes.CarpetaCartas` (campo privado) apunta a `assets/cartas/mazo_basico` — usado por `CargarCartaAsync`, que ya existe y no cambia de firma.

- [ ] **Step 1: Mover los archivos**

```bash
mkdir -p "src/TrucoUruguayo.Bot/assets/cartas/mazo_basico"
git mv src/TrucoUruguayo.Bot/assets/cartas/*.png src/TrucoUruguayo.Bot/assets/cartas/mazo_basico/
git mv src/TrucoUruguayo.Bot/assets/cartas/ATRIBUCION.md src/TrucoUruguayo.Bot/assets/cartas/mazo_basico/ATRIBUCION.md
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter GeneradorImagenesTests`
Expected: FAIL — los 3 tests de `GeneradorImagenesTests` fallan porque `CarpetaCartas` sigue apuntando a `assets/cartas` (ya no hay archivos `.png` ahí, se movieron a `mazo_basico/`).

- [ ] **Step 3: Actualizar las rutas**

En `src/TrucoUruguayo.Bot/Servicios/GeneradorImagenes.cs`, reemplazar:

```csharp
    private static readonly string CarpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas");
```

por:

```csharp
    private static readonly string CarpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas", "mazo_basico");
```

En `tests/TrucoUruguayo.Bot.Tests/Servicios/GeneradorImagenesTests.cs`, reemplazar:

```csharp
        var carpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas");
```

por:

```csharp
        var carpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas", "mazo_basico");
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter GeneradorImagenesTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Correr toda la suite del Bot**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: PASS (toda la suite — nada más referencia la ruta vieja).

- [ ] **Step 6: Commit**

```bash
git add -A src/TrucoUruguayo.Bot/assets/cartas src/TrucoUruguayo.Bot/Servicios/GeneradorImagenes.cs tests/TrucoUruguayo.Bot.Tests/Servicios/GeneradorImagenesTests.cs
git commit -m "$(cat <<'EOF'
refactor: mueve el mazo actual a assets/cartas/mazo_basico

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Traer el mazo clásico y el dorso desde el repo externo

**Files:**
- Create: `src/TrucoUruguayo.Bot/assets/cartas/mazo_clasico/{numero}_{palo}.jpg` (40 archivos)
- Create: `src/TrucoUruguayo.Bot/assets/cartas/mazo_clasico/ATRIBUCION.md`
- Create: `src/TrucoUruguayo.Bot/assets/cartas/dorso.png`

**Interfaces:**
- Consumes: nada (assets puros, sin código todavía referenciándolos — eso es la Task 4).
- Produces: los 40 archivos y el dorso quedan disponibles en disco para que Task 4 los use.

Sin tests automatizados (son archivos, no código). Se verifica con `dotnet build` + suite completa (no debería cambiar nada, ya que nada los referencia todavía).

- [ ] **Step 1: Clonar el repo externo a una carpeta temporal y copiar los archivos**

```bash
TMPDIR=$(mktemp -d)
git clone --depth 1 https://github.com/RolandoAndrade/juego-truco.git "$TMPDIR"

DEST="src/TrucoUruguayo.Bot/assets/cartas/mazo_clasico"
mkdir -p "$DEST"

declare -A PALOS=( [sword]=espada [cup]=copa [gold]=oro [course]=basto )
for carpeta in "${!PALOS[@]}"; do
  palo="${PALOS[$carpeta]}"
  for numero in 1 2 3 4 5 6 7 10 11 12; do
    cp "$TMPDIR/UILayer/src/main/resources/$carpeta/$numero.jpeg" "$DEST/${numero}_${palo}.jpg"
  done
done

cp "$TMPDIR/UILayer/src/main/resources/back.png" "src/TrucoUruguayo.Bot/assets/cartas/dorso.png"

rm -rf "$TMPDIR"
```

- [ ] **Step 2: Verificar que se copiaron los 40 archivos**

Run: `ls src/TrucoUruguayo.Bot/assets/cartas/mazo_clasico | wc -l`
Expected: `40`

- [ ] **Step 3: Crear la atribución**

Crear `src/TrucoUruguayo.Bot/assets/cartas/mazo_clasico/ATRIBUCION.md`:

```markdown
# Atribución de las imágenes de este mazo

Las 40 imágenes de esta carpeta (`{numero}_{palo}.jpg`) provienen del repositorio
[RolandoAndrade/juego-truco](https://github.com/RolandoAndrade/juego-truco)
(`UILayer/src/main/resources/{sword,cup,gold,course}/`), un proyecto de cátedra
("Redes de Computadores I") de Rolando Andrade y José Cedeño.

- **Licencia:** el repositorio no incluye un archivo `LICENSE`. El diseño de las
  cartas es un estilo tradicional de baraja española genérico, no parece arte
  original de los autores del repo — pero no hay una licencia explícita que citar.
  Si en algún momento se identifica la fuente original de este diseño, actualizar
  esta atribución.
- **Cambios realizados:** se reorganizaron los archivos de `{palo_en_ingles}/{numero}.jpeg`
  a `{numero}_{palo}.jpg` para seguir la misma convención que `mazo_basico/`. No se
  modificó el contenido visual de las cartas.

## Dorso (`assets/cartas/dorso.png`)

También sale de este mismo repositorio (`UILayer/src/main/resources/back.png`) y
se usa compartido entre mazos — mismas condiciones de licencia que arriba.
```

- [ ] **Step 4: Compilar y correr toda la suite**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: build sin errores, toda la suite en verde (estos archivos todavía no los usa ningún código).

- [ ] **Step 5: Commit**

```bash
git add src/TrucoUruguayo.Bot/assets/cartas/mazo_clasico src/TrucoUruguayo.Bot/assets/cartas/dorso.png
git commit -m "$(cat <<'EOF'
feat: agrega el mazo clasico y el dorso compartido

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Arreglar el nombre del fondo de madera y copiarlo al build

**Files:**
- Modify (renombrar): `src/TrucoUruguayo.Bot/assets/fondos/fondo_madera.jpg!sw800` → `src/TrucoUruguayo.Bot/assets/fondos/fondo_madera.jpg`
- Modify: `src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj`

**Interfaces:**
- Consumes: nada.
- Produces: `assets/fondos/fondo_madera.jpg` queda en el directorio de salida del build — lo usa `GeneradorImagenes` en la Task 4.

Sin tests automatizados. Se verifica confirmando que el archivo aparece en el directorio de salida después de compilar.

- [ ] **Step 1: Renombrar el archivo**

El archivo está sin trackear en git (nunca se commiteó), así que alcanza con un `mv` normal:

```bash
mv "src/TrucoUruguayo.Bot/assets/fondos/fondo_madera.jpg!sw800" "src/TrucoUruguayo.Bot/assets/fondos/fondo_madera.jpg"
```

- [ ] **Step 2: Agregar la carpeta al `.csproj`**

En `src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj`, reemplazar:

```xml
  <ItemGroup>
    <None Include="assets\cartas\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

por:

```xml
  <ItemGroup>
    <None Include="assets\cartas\**" CopyToOutputDirectory="PreserveNewest" />
    <None Include="assets\fondos\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 3: Compilar y confirmar que el archivo llega al output**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && find src/TrucoUruguayo.Bot/bin/Debug/net10.0/assets/fondos -iname "fondo_madera.jpg"`
Expected: build sin errores, el `find` encuentra `fondo_madera.jpg` dentro de `bin/Debug/net10.0/assets/fondos/`.

- [ ] **Step 4: Correr toda la suite**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: PASS (toda la suite — nada lo usa todavía).

- [ ] **Step 5: Commit**

```bash
git add src/TrucoUruguayo.Bot/assets/fondos src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj
git commit -m "$(cat <<'EOF'
fix: arregla el nombre del fondo de madera y lo copia al build

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Dorso bajo la muestra y fondo de madera en la mesa

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Servicios/GeneradorImagenes.cs`
- Test: `tests/TrucoUruguayo.Bot.Tests/Servicios/GeneradorImagenesTests.cs`

**Interfaces:**
- Consumes (de Task 2 y 3): `assets/cartas/dorso.png`, `assets/fondos/fondo_madera.jpg` (rutas fijas en disco).
- Produces: `GeneradorImagenes.GenerarMesaActualAsync(Carta muestra, Carta? jugada1, Carta? jugada2) -> Task<MemoryStream>` sigue con la misma firma — cambia el contenido de la imagen que devuelve, no su forma de llamarse.

- [ ] **Step 1: Actualizar los tests existentes para el ancho nuevo**

`GeneradorImagenesTests.cs` ya prueba anchos exactos de `GenerarMesaActualAsync`; con la pila de dorso, el grupo "muestra + pila" crece `12px` (`MargenPilaDorso=6 * CantidadDorsosEnPila=2`) en cada dimensión. Reemplazar:

```csharp
public class GeneradorImagenesTests
{
    private const int AnchoCartaEsperado = 100;
    private const int AnchoMuestraEsperado = 75;
    private const int AnchoJugadaEsperado = 110;

    [Fact]
    public async Task GenerarMesaActualAsync_SoloMuestra_DevuelveUnPngDelAnchoRedimensionado()
    {
        var generador = new GeneradorImagenes();

        await using var stream = await generador.GenerarMesaActualAsync(new Carta(1, Palo.Espada), null, null);

        Assert.Equal(0, stream.Position);
        using var imagen = await Image.LoadAsync(stream);
        Assert.Equal(AnchoMuestraEsperado, imagen.Width);
        Assert.True(imagen.Height > 0);
    }

    [Fact]
    public async Task GenerarMesaActualAsync_ConLasDosJugadas_ElAnchoIncluyeMuestraYAmbasCartas()
    {
        var generador = new GeneradorImagenes();

        await using var stream = await generador.GenerarMesaActualAsync(
            new Carta(3, Palo.Oro), new Carta(1, Palo.Espada), new Carta(7, Palo.Copa));

        using var imagen = await Image.LoadAsync(stream);

        // muestra (75px, mas chica) + margen de grupo (30) + 2 jugadas de 110px + margen entre ellas (10)
        var anchoEsperado = AnchoMuestraEsperado + 30 + AnchoJugadaEsperado + 10 + AnchoJugadaEsperado;
        Assert.Equal(anchoEsperado, imagen.Width);
    }
```

por:

```csharp
public class GeneradorImagenesTests
{
    private const int AnchoCartaEsperado = 100;
    private const int AnchoMuestraEsperado = 75;
    private const int AnchoJugadaEsperado = 110;
    private const int ExtraPilaDorsoEsperado = 12; // MargenPilaDorso (6) * CantidadDorsosEnPila (2)

    [Fact]
    public async Task GenerarMesaActualAsync_SoloMuestra_DevuelveUnPngDelAnchoRedimensionado()
    {
        var generador = new GeneradorImagenes();

        await using var stream = await generador.GenerarMesaActualAsync(new Carta(1, Palo.Espada), null, null);

        Assert.Equal(0, stream.Position);
        using var imagen = await Image.LoadAsync(stream);
        Assert.Equal(AnchoMuestraEsperado + ExtraPilaDorsoEsperado, imagen.Width);
        Assert.True(imagen.Height > 0);
    }

    [Fact]
    public async Task GenerarMesaActualAsync_ConLasDosJugadas_ElAnchoIncluyeMuestraYAmbasCartas()
    {
        var generador = new GeneradorImagenes();

        await using var stream = await generador.GenerarMesaActualAsync(
            new Carta(3, Palo.Oro), new Carta(1, Palo.Espada), new Carta(7, Palo.Copa));

        using var imagen = await Image.LoadAsync(stream);

        // muestra (75px, mas chica) + pila de dorso detras + margen de grupo (30) + 2 jugadas de 110px + margen entre ellas (10)
        var anchoEsperado = AnchoMuestraEsperado + ExtraPilaDorsoEsperado + 30 + AnchoJugadaEsperado + 10 + AnchoJugadaEsperado;
        Assert.Equal(anchoEsperado, imagen.Width);
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter GeneradorImagenesTests`
Expected: FAIL — los dos tests de `GenerarMesaActualAsync` fallan porque el código todavía no agrega los `12px` extra.

- [ ] **Step 3: Implementar la pila de dorso y el fondo de madera**

En `src/TrucoUruguayo.Bot/Servicios/GeneradorImagenes.cs`, reemplazar:

```csharp
public class GeneradorImagenes
{
    private const int AnchoCarta = 100;
    private const int AnchoMuestraEnMesa = 75;
    private const int AnchoJugadaEnMesa = 110;
    private const int MargenEntreCartas = 10;
    private const int MargenEntreGrupos = 30;

    private static readonly string CarpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas", "mazo_basico");
```

por:

```csharp
public class GeneradorImagenes
{
    private const int AnchoCarta = 100;
    private const int AnchoMuestraEnMesa = 75;
    private const int AnchoJugadaEnMesa = 110;
    private const int MargenEntreCartas = 10;
    private const int MargenEntreGrupos = 30;
    private const int MargenPilaDorso = 6;
    private const int CantidadDorsosEnPila = 2;

    private static readonly string CarpetaCartas = Path.Combine(AppContext.BaseDirectory, "assets", "cartas", "mazo_basico");
    private static readonly string RutaDorso = Path.Combine(AppContext.BaseDirectory, "assets", "cartas", "dorso.png");
    private static readonly string RutaFondoMadera = Path.Combine(AppContext.BaseDirectory, "assets", "fondos", "fondo_madera.jpg");
```

Reemplazar el cuerpo completo de `GenerarMesaActualAsync`:

```csharp
    public async Task<MemoryStream> GenerarMesaActualAsync(Carta muestra, Carta? jugada1, Carta? jugada2)
    {
        using var imagenMuestra = await CargarCartaAsync(muestra, AnchoMuestraEnMesa);
        using var imagenJugada1 = jugada1 is not null ? await CargarCartaAsync(jugada1, AnchoJugadaEnMesa) : null;
        using var imagenJugada2 = jugada2 is not null ? await CargarCartaAsync(jugada2, AnchoJugadaEnMesa) : null;

        var jugadas = new List<Image<Rgba32>>();
        if (imagenJugada1 is not null)
        {
            jugadas.Add(imagenJugada1);
        }

        if (imagenJugada2 is not null)
        {
            jugadas.Add(imagenJugada2);
        }

        var anchoJugadas = jugadas.Sum(imagen => imagen.Width) + MargenEntreCartas * Math.Max(0, jugadas.Count - 1);
        var espacioAntesDeJugadas = jugadas.Count > 0 ? MargenEntreGrupos : 0;
        var anchoTotal = imagenMuestra.Width + espacioAntesDeJugadas + anchoJugadas;
        var altoMaximo = Math.Max(imagenMuestra.Height, jugadas.Count > 0 ? jugadas.Max(imagen => imagen.Height) : 0);

        using var lienzo = new Image<Rgba32>(anchoTotal, altoMaximo);

        lienzo.Mutate(contexto =>
        {
            // La muestra es mas chica que las jugadas: se centra verticalmente, lo que la
            // deja un poco mas abajo que el borde superior donde arrancan las jugadas.
            contexto.DrawImage(imagenMuestra, new Point(0, (altoMaximo - imagenMuestra.Height) / 2), 1f);

            var posicionX = imagenMuestra.Width + espacioAntesDeJugadas;
            foreach (var imagenJugada in jugadas)
            {
                contexto.DrawImage(imagenJugada, new Point(posicionX, (altoMaximo - imagenJugada.Height) / 2), 1f);
                posicionX += imagenJugada.Width + MargenEntreCartas;
            }
        });

        var streamResultado = new MemoryStream();
        await lienzo.SaveAsPngAsync(streamResultado);
        streamResultado.Position = 0;
        return streamResultado;
    }
```

por:

```csharp
    public async Task<MemoryStream> GenerarMesaActualAsync(Carta muestra, Carta? jugada1, Carta? jugada2)
    {
        using var imagenMuestra = await CargarCartaAsync(muestra, AnchoMuestraEnMesa);
        using var imagenDorso = await CargarDorsoAsync(AnchoMuestraEnMesa);
        using var imagenJugada1 = jugada1 is not null ? await CargarCartaAsync(jugada1, AnchoJugadaEnMesa) : null;
        using var imagenJugada2 = jugada2 is not null ? await CargarCartaAsync(jugada2, AnchoJugadaEnMesa) : null;

        var jugadas = new List<Image<Rgba32>>();
        if (imagenJugada1 is not null)
        {
            jugadas.Add(imagenJugada1);
        }

        if (imagenJugada2 is not null)
        {
            jugadas.Add(imagenJugada2);
        }

        // La pila de dorso asoma detras de la muestra (2 copias desplazadas), simulando el
        // resto del mazo boca abajo debajo de la carta dada vuelta. Eso agranda el grupo
        // "muestra + pila" en el margen maximo de la pila, en ancho y alto.
        var extraPila = MargenPilaDorso * CantidadDorsosEnPila;
        var anchoMuestraConPila = imagenMuestra.Width + extraPila;
        var altoMuestraConPila = imagenMuestra.Height + extraPila;

        var anchoJugadas = jugadas.Sum(imagen => imagen.Width) + MargenEntreCartas * Math.Max(0, jugadas.Count - 1);
        var espacioAntesDeJugadas = jugadas.Count > 0 ? MargenEntreGrupos : 0;
        var anchoTotal = anchoMuestraConPila + espacioAntesDeJugadas + anchoJugadas;
        var altoMaximo = Math.Max(altoMuestraConPila, jugadas.Count > 0 ? jugadas.Max(imagen => imagen.Height) : 0);

        using var lienzo = new Image<Rgba32>(anchoTotal, altoMaximo);
        using var imagenFondo = await CargarFondoMaderaAsync(anchoTotal, altoMaximo);

        lienzo.Mutate(contexto =>
        {
            contexto.DrawImage(imagenFondo, new Point(0, 0), 1f);

            // La muestra es mas chica que las jugadas: se centra verticalmente, lo que la
            // deja un poco mas abajo que el borde superior donde arrancan las jugadas.
            var baseYMuestra = (altoMaximo - altoMuestraConPila) / 2;

            for (var i = CantidadDorsosEnPila; i >= 1; i--)
            {
                contexto.DrawImage(imagenDorso, new Point(MargenPilaDorso * i, baseYMuestra + MargenPilaDorso * i), 1f);
            }

            contexto.DrawImage(imagenMuestra, new Point(0, baseYMuestra), 1f);

            var posicionX = anchoMuestraConPila + espacioAntesDeJugadas;
            foreach (var imagenJugada in jugadas)
            {
                contexto.DrawImage(imagenJugada, new Point(posicionX, (altoMaximo - imagenJugada.Height) / 2), 1f);
                posicionX += imagenJugada.Width + MargenEntreCartas;
            }
        });

        var streamResultado = new MemoryStream();
        await lienzo.SaveAsPngAsync(streamResultado);
        streamResultado.Position = 0;
        return streamResultado;
    }
```

Agregar estos dos métodos privados nuevos, justo después de `CargarCartaAsync`:

```csharp
    private static async Task<Image<Rgba32>> CargarDorsoAsync(int ancho)
    {
        var imagen = await Image.LoadAsync<Rgba32>(RutaDorso);
        imagen.Mutate(x => x.Resize(ancho, 0));
        return imagen;
    }

    private static async Task<Image<Rgba32>> CargarFondoMaderaAsync(int ancho, int alto)
    {
        var imagen = await Image.LoadAsync<Rgba32>(RutaFondoMadera);
        imagen.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(ancho, alto),
            Mode = ResizeMode.Cover,
        }));
        return imagen;
    }
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter GeneradorImagenesTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Correr toda la suite del Bot y del Core**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj && dotnet test tests/TrucoUruguayo.Core.Tests`
Expected: PASS (toda la suite).

- [ ] **Step 6: Verificación visual manual**

Los tests automatizados solo chequean tamaños, no cómo se ve la imagen — hay que mirarla. Generar una mesa de ejemplo y guardarla a disco para revisarla:

Crear un archivo descartable `tests/TrucoUruguayo.Bot.Tests/_VerificacionVisual.cs`:

```csharp
using TrucoUruguayo.Bot.Servicios;
using TrucoUruguayo.Core.Modelo;
using Xunit;

namespace TrucoUruguayo.Bot.Tests.Servicios;

public class _VerificacionVisual
{
    [Fact]
    public async Task GenerarMuestraDeMesa()
    {
        var generador = new GeneradorImagenes();
        await using var stream = await generador.GenerarMesaActualAsync(
            new Carta(2, Palo.Oro), new Carta(1, Palo.Espada), new Carta(7, Palo.Copa));

        await using var archivo = File.Create(Path.Combine(Path.GetTempPath(), "mesa_verificacion.png"));
        await stream.CopyToAsync(archivo);
    }
}
```

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter _VerificacionVisual`
Expected: PASS (1 test), y queda un archivo en la carpeta temporal del sistema (`%TEMP%\mesa_verificacion.png` en Windows).

Abrir/mirar ese PNG (con la herramienta de lectura de imágenes) y confirmar: se ve el fondo de madera cubriendo todo el lienzo, la muestra con el dorso asomando detrás (abajo a la derecha), y las dos jugadas a la derecha sin superponerse con nada. Si algo se ve mal (colores raros, recortes, la pila tapando la muestra en vez de asomar detrás), ajustar `MargenPilaDorso`/`CantidadDorsosEnPila` y repetir.

Después, **borrar el archivo** `tests/TrucoUruguayo.Bot.Tests/_VerificacionVisual.cs` — es descartable, no se commitea.

- [ ] **Step 7: Commit**

```bash
git add src/TrucoUruguayo.Bot/Servicios/GeneradorImagenes.cs tests/TrucoUruguayo.Bot.Tests/Servicios/GeneradorImagenesTests.cs
git commit -m "$(cat <<'EOF'
feat: agrega dorso bajo la muestra y fondo de madera a la mesa

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
