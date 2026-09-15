# Experiencia (XP), Niveles y Barra de Progreso

## Contexto

Hoy `usuarios` ya tiene una columna `xp` (`Usuario.Xp` en el modelo) que `UsuarioRepository.SumarVictoriaAsync`/`SumarDerrotaAsync` incrementan en +15/+3 junto con `victorias`/`derrotas`. Esas dos llamadas se disparan en dos lugares distintos:

- `GestorPartidas.ChequearInactividadAsync` (fin de partida por AFK).
- `TrucoModule.FinalizarYAnunciarRonda` (fin de partida normal — **no** vive en `GestorPartidas`).

No existe ningún concepto de "nivel", ni una barra de progreso. `PerfilModule` hoy muestra el XP crudo en un field "✨ XP".

Este spec agrega niveles calculados a partir del XP acumulado, cambia el balanceo de XP por partida (+50 ganador / +15 perdedor, reemplazando el +15/+3 actual) y agrega una barra de progreso visual en `/perfil`.

## Fórmula de nivel

Costo de subir de `nivel` a `nivel + 1`: `nivel * 100` XP (Nivel 1→2 = 100, Nivel 2→3 = 200, etc.).

XP acumulada necesaria para **llegar** a un nivel `N` (partiendo de nivel 1 con 0 XP):

```
XpParaAlcanzarNivel(N) = 100 * (N - 1) * N / 2
```

(`XpParaAlcanzarNivel(1) = 0`, `(2) = 100`, `(3) = 300`, `(4) = 600`, ...)

Esta fórmula vive en un helper estático compartido `NivelCalculadora` (nuevo, `src/TrucoUruguayo.Bot/Modelo/NivelCalculadora.cs`) porque la necesitan tanto `UsuarioRepository.SumarExpAsync` (para decidir cuándo subir de nivel) como `PerfilModule` (para la barra).

## Schema y modelo

`src/TrucoUruguayo.Bot/Datos/schema.sql`: agregar `nivel INTEGER NOT NULL DEFAULT 1` a la definición de `CREATE TABLE usuarios`, más una línea `ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS nivel INTEGER NOT NULL DEFAULT 1;` justo debajo — no hay runner de migraciones en este repo (schema.sql se corre a mano), así que el `ALTER` idempotente es lo que hace que re-ejecutar el script contra una base ya existente (incluida la Postgres local de desarrollo) agregue la columna sin romper nada. Cualquier base separada (ej. producción) necesita que alguien corra ese mismo `ALTER` a mano.

`src/TrucoUruguayo.Bot/Modelo/Usuario.cs`: agregar `public int Nivel { get; set; }`.

## `UsuarioRepository.cs`

- `ObtenerUsuarioAsync`, `RegistrarUsuarioAsync` (RETURNING) y `ObtenerTopUsuariosAsync`: sus `SELECT`/`RETURNING` agregan `nivel AS Nivel`. El `INSERT` de `RegistrarUsuarioAsync` no necesita tocarse: al no listar `nivel`, Postgres usa el `DEFAULT 1` de la columna.
- `SumarVictoriaAsync`/`SumarDerrotaAsync`: se les saca el `xp = xp + N` del `UPDATE` — quedan sumando solo `victorias`/`derrotas`. El XP pasa a otorgarse exclusivamente vía `SumarExpAsync`, evitando el doble conteo.
- Nuevo método:

```csharp
public async Task<(bool SubioDeNivel, int NuevoNivel)> SumarExpAsync(ulong discordId, int cantidadExp)
```

Implementación: abre una transacción, hace `SELECT xp, nivel FROM usuarios WHERE id = @Id FOR UPDATE` (mismo patrón de lock que ya usa `ReclamarDiariaAsync`), suma `cantidadExp` al xp leído, y con un `while (nuevoXp >= NivelCalculadora.XpParaAlcanzarNivel(nuevoNivel + 1)) nuevoNivel++;` calcula el nivel resultante (el `while`, no un solo `if`, cubre el caso remoto de que una sola entrega de XP cruce más de un umbral). Persiste `xp` y `nivel` con un `UPDATE`, commitea, y devuelve `(nuevoNivel > nivelOriginal, nuevoNivel)`.

## Reparto de XP al finalizar una partida

En **ambos** puntos donde hoy se llama a `SumarVictoriaAsync`/`SumarDerrotaAsync` — `GestorPartidas.ChequearInactividadAsync` y `TrucoModule.FinalizarYAnunciarRonda` — se agrega, después de esas llamadas:

```csharp
var (subioGanador, nivelGanador) = await _usuarioRepository.SumarExpAsync(ganadorId, 50);
var (subioPerdedor, nivelPerdedor) = await _usuarioRepository.SumarExpAsync(perdedorId, 15);
```

(en `ChequearInactividadAsync` los IDs son `ganadorId`/`afkId`). Después de enviar el mensaje de fin de partida existente, si `subioGanador` o `subioPerdedor` es `true`, se manda un mensaje adicional al mismo canal por cada jugador que subió:

```csharp
$"🎉 ¡Felicidades <@{jugadorId}>! Has alcanzado el **Nivel {nuevoNivel}**."
```

## `PerfilModule.cs`

Helper privado (verbatim, ya provisto):

```csharp
private string GenerarBarraExp(int expActual, int expNecesaria, int longitudBarra = 10)
{
    double porcentaje = Math.Clamp((double)expActual / expNecesaria, 0, 1);
    int bloquesLlenos = (int)Math.Round(porcentaje * longitudBarra);
    int bloquesVacios = longitudBarra - bloquesLlenos;
    return $"[{new string('█', bloquesLlenos)}{new string('░', bloquesVacios)}] {expActual}/{expNecesaria} XP";
}
```

En `PerfilAsync`, con `usuarioDb.Xp` y `usuarioDb.Nivel` ya cargados:

```csharp
var xpNivelActual = usuarioDb.Xp - NivelCalculadora.XpParaAlcanzarNivel(usuarioDb.Nivel);
var xpNecesaria = usuarioDb.Nivel * 100;
```

El field `"✨ XP"` actual se **reemplaza** por uno nuevo:

```csharp
.AddField("🎮 Nivel y Experiencia", $"**Nivel {usuarioDb.Nivel}**\n{GenerarBarraExp(xpNivelActual, xpNecesaria)}")
```

(mostrar el XP crudo total junto a la barra sería redundante — la barra ya lo comunica).

## Testing

`tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs` (integración contra Postgres real, mismo patrón que el resto del archivo — `UsuarioDePrueba`/`ConexionDePrueba`):

- Actualizar `SumarVictoriaAsync_SumaUnaVictoriaYQuinceXp` y `SumarDerrotaAsync_SumaUnaDerrotaYTresXp`: ya no suman XP, así que se les saca el `Assert.Equal(N, actualizado.Xp)` (o se agrega `Assert.Equal(0, actualizado.Xp)` para dejar explícito que no tocan XP).
- Actualizar `ObtenerTopUsuariosAsync_OrdenXp_OrdenaPorXpDescendente`: hoy genera la diferencia de XP llamando `SumarVictoriaAsync` varias veces; pasa a usar `SumarExpAsync` directamente.
- Nuevos casos para `SumarExpAsync`:
  - Suma XP sin cruzar el umbral del próximo nivel → `SubioDeNivel = false`, nivel sin cambios, XP persistido.
  - Suma XP que cruza el umbral (ej. usuario en nivel 1 recibe 150 XP, cruza los 100 necesarios) → `SubioDeNivel = true`, `NuevoNivel = 2`.
  - Suma XP que cruza más de un umbral de una sola vez (ej. usuario en nivel 1 recibe 500 XP, que supera tanto el umbral a nivel 2 (100) como a nivel 3 (300)) → `NuevoNivel = 3`.

No se agregan tests automatizados para los handlers de Discord (`GestorPartidas.ChequearInactividadAsync`, `TrucoModule.FinalizarYAnunciarRonda`, `PerfilModule.PerfilAsync`) — siguen sin cobertura unitaria en este repo, igual que el resto de esos flujos.
