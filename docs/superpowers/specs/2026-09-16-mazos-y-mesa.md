# Mazo básico/clásico organizados, dorso bajo la muestra y fondo de madera en la mesa

## Contexto

Hoy `assets/cartas/` es una carpeta plana con las 40 cartas del único mazo que existe, y `GeneradorImagenes` la referencia con una ruta hardcodeada. No hay concepto de "mazo" ni de dorso de carta, y el lienzo de la mesa se genera con fondo transparente. El usuario agregó un fondo de madera (`fondo_madera.jpg!sw800`, con la extensión rota) y pidió organizar un segundo mazo tomado de un repo externo (que sí trae dorso).

Alcance acordado: **solo organizar los assets y mejorar la imagen de mesa** — el bot sigue usando un único mazo activo ("básico"); elegir entre mazos queda para una feature futura aparte.

## A) Reorganización de assets

### Mazo básico (el que ya existe)

Mover, sin modificar contenido:

- `assets/cartas/*.png` (40 archivos) → `assets/cartas/mazo_basico/*.png`
- `assets/cartas/ATRIBUCION.md` → `assets/cartas/mazo_basico/ATRIBUCION.md`

### Mazo clásico (nuevo, desde `https://github.com/RolandoAndrade/juego-truco.git`)

El repo (proyecto Java de cátedra, sin archivo `LICENSE`) tiene el mazo completo en `UILayer/src/main/resources/`, en carpetas por palo con nombres en inglés: `sword` (espada), `cup` (copa), `gold` (oro), `course` (basto — confirmado por el mapeo `"course" → "B"` en `Serial/SerialManager.java`), cada una con los 10 números que usa el Truco (1,2,3,4,5,6,7,10,11,12), en `.jpeg`. También tiene `back.png`, el dorso clásico rojo estilo Bicycle.

Copiar a `assets/cartas/mazo_clasico/`, renombrando `{palo_en_ingles}/{numero}.jpeg` → `{numero}_{palo}.jpg` (mismo esquema de nombres que el mazo básico, con extensión `.jpg` porque el contenido ya es JPEG — no hace falta reconvertir a PNG).

Crear `assets/cartas/mazo_clasico/ATRIBUCION.md`:

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
```

### Dorso compartido

Copiar `back.png` del mismo repo a `assets/cartas/dorso.png` (no es específico de ningún mazo — lo usa el efecto visual de la mesa sin importar qué mazo esté activo, ya que `mazo_basico` no trae uno propio). Agregar la atribución del dorso al mismo `ATRIBUCION.md` de `mazo_clasico` (es de ahí que sale):

```markdown

## Dorso (`assets/cartas/dorso.png`)

También sale de este mismo repositorio (`UILayer/src/main/resources/back.png`) y
se usa compartido entre mazos — mismas condiciones de licencia que arriba.
```

### Fondo de madera

Renombrar `assets/fondos/fondo_madera.jpg!sw800` → `assets/fondos/fondo_madera.jpg` (la extensión traía un parámetro de URL pegado, ej. de un thumbnail `?sw800`, y así no se puede referenciar por extensión de forma prolija).

## B) `GeneradorImagenes.cs`: mazo activo, dorso bajo la muestra, fondo de mesa

`CarpetaCartas` pasa de `assets/cartas` a `assets/cartas/mazo_basico` (el mazo activo sigue siendo uno solo, ahora vive en su propia carpeta). Se agregan dos rutas nuevas: `assets/cartas/dorso.png` y `assets/fondos/fondo_madera.jpg`.

**Dorso bajo la muestra**: en `GenerarMesaActualAsync`, además de la muestra, se cargan 2 copias del dorso (mismo ancho que la muestra, `AnchoMuestraEnMesa`) y se dibujan detrás de ella con un desplazamiento de 6px cada una (hacia abajo y a la derecha), simulando el mazo boca abajo asomando debajo de la muestra — igual que en la imagen de referencia. Esto agrega `6 * 2 = 12px` de ancho y de alto al grupo de la muestra (el desplazamiento máximo de la pila).

**Fondo de madera**: el lienzo de `GenerarMesaActualAsync` deja de crearse transparente — se dibuja primero la imagen de `fondo_madera.jpg` redimensionada para cubrir el canvas completo (`ResizeMode.Cover`, sin deformarse) y recién después, encima, la pila de dorso + muestra + jugadas. `GenerarManoAsync` no se toca (queda con fondo transparente, como hoy).

## Testing

`tests/TrucoUruguayo.Bot.Tests/Servicios/GeneradorImagenesTests.cs` ya existe y hoy:

- Carga cartas reales directo desde `Path.Combine(AppContext.BaseDirectory, "assets", "cartas")` sin subcarpeta — hay que actualizar esa ruta a `mazo_basico`.
- Asume que el ancho de la mesa con solo la muestra es exactamente `AnchoMuestraEsperado` (75px) — ahora son `75 + 12 = 87px` por la pila de dorso.
- Asume que el ancho de la mesa con las dos jugadas es `AnchoMuestraEsperado + 30 + ...` — ahora es `(75 + 12) + 30 + ...`.

Ningún test verifica el fondo (solo dimensiones), así que agregar el fondo de madera no rompe nada por sí solo.

**Confirmado en `TrucoUruguayo.Bot.csproj`**: hoy solo tiene `<None Include="assets\cartas\**" CopyToOutputDirectory="PreserveNewest" />`. El `**` ya cubre subcarpetas, así que `mazo_basico/`, `mazo_clasico/` y `dorso.png` (todos dentro de `assets\cartas\`) se copian solos, sin tocar el `.csproj`. Pero **`assets\fondos\` no tiene ninguna entrada** — hay que agregar `<None Include="assets\fondos\**" CopyToOutputDirectory="PreserveNewest" />`, si no `fondo_madera.jpg` no llega al directorio de salida y ni la app ni los tests lo van a encontrar.

`assets/muestra_bajocarta.png` (la imagen de referencia que mandó el usuario) no se toca — no es un asset que consuma el código, queda donde está.

No se agrega ningún mecanismo para elegir mazo — queda fuera de este spec, según lo acordado.
