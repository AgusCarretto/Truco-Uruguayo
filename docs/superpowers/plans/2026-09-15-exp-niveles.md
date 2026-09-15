# Experiencia (XP), Niveles y Barra de Progreso — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cada partida otorga XP (+50 ganador, +15 perdedor), el XP acumulado determina un nivel calculable, y `/perfil` muestra una barra de progreso visual en vez del XP crudo.

**Architecture:** Un helper puro `NivelCalculadora` calcula el umbral de XP de cada nivel; `UsuarioRepository.SumarExpAsync` lockea la fila del usuario, suma XP y recalcula el nivel en una transacción; `GestorPartidas` (fin por AFK) y `TrucoModule` (fin normal) llaman a ese método después de pagar monedas y avisan por Discord si alguien subió de nivel; `PerfilModule` usa el mismo helper para renderizar la barra.

**Tech Stack:** C# / .NET 10, Npgsql + Dapper, Discord.Net.Interactions 3.20.1, xUnit (tests de `UsuarioRepository` son de integración contra Postgres real).

## Global Constraints

- Fórmula: subir de `nivel` a `nivel + 1` cuesta `nivel * 100` XP. XP acumulado para *llegar* al nivel `N`: `XpParaAlcanzarNivel(N) = 100 * (N - 1) * N / 2`.
- Balanceo por partida: **+50 XP al ganador, +15 XP al perdedor**, y esto **reemplaza** (no se suma a) el +15/+3 que hoy otorgan `SumarVictoriaAsync`/`SumarDerrotaAsync`.
- Se reutiliza la columna/propiedad `xp`/`Usuario.Xp` que ya existe — no se crea una columna `exp` separada.
- No hay runner de migraciones en este repo: `schema.sql` se corre a mano contra cada base. El `ALTER TABLE ... ADD COLUMN IF NOT EXISTS nivel ...` debe aplicarse contra la Postgres local de desarrollo para que los tests de integración corran, y contra cualquier otra base (ej. producción) por separado.
- En `/perfil`, el field `"✨ XP"` actual se reemplaza por `"🎮 Nivel y Experiencia"` (no coexisten).
- `GenerarBarraExp` se usa tal cual fue especificada, sin modificaciones.

Spec completo: `docs/superpowers/specs/2026-09-15-exp-niveles-design.md`

---

### Task 1: Columna `nivel`, modelo, `NivelCalculadora` y las consultas existentes

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Datos/schema.sql`
- Modify: `src/TrucoUruguayo.Bot/Modelo/Usuario.cs`
- Create: `src/TrucoUruguayo.Bot/Modelo/NivelCalculadora.cs`
- Modify: `src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs:18-44` (`ObtenerUsuarioAsync`, `RegistrarUsuarioAsync`) y `:73-87` (`ObtenerTopUsuariosAsync`)
- Test: `tests/TrucoUruguayo.Bot.Tests/Modelo/NivelCalculadoraTests.cs` (nuevo)
- Test: `tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs:11-26` (extender test existente)

**Interfaces:**
- Consumes: nada nuevo.
- Produces (usado por Task 2, 4 y 5):
  - `NivelCalculadora.XpParaAlcanzarNivel(int nivel) -> int` (estático, `TrucoUruguayo.Bot.Modelo`).
  - `Usuario.Nivel` (`int`, `get; set;`).
  - `ObtenerUsuarioAsync`, `RegistrarUsuarioAsync` y `ObtenerTopUsuariosAsync` ahora populan `Nivel` en el `Usuario` devuelto.

- [ ] **Step 1: Aplicar el cambio de schema contra tu Postgres local**

No hay runner de migraciones — hay que correr el `ALTER` una sola vez contra la base que usan los tests de integración (la de `src/TrucoUruguayo.Bot/.env`). Como este repo no tiene `psql` como dependencia, usá el propio toolchain de tests (Npgsql, ya referenciado) con un archivo descartable:

Crear `tests/TrucoUruguayo.Bot.Tests/_AplicarMigracionNivel.cs`:

```csharp
using Npgsql;
using TrucoUruguayo.Bot.Tests.Helpers;
using Xunit;

namespace TrucoUruguayo.Bot.Tests.Datos;

public class _AplicarMigracionNivel
{
    [Fact]
    public async Task Aplicar()
    {
        var connectionString = ConexionDePrueba.ObtenerConnectionString();
        await using var conexion = new NpgsqlConnection(connectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(
            "ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS nivel INTEGER NOT NULL DEFAULT 1;",
            conexion);
        await comando.ExecuteNonQueryAsync();
    }
}
```

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter _AplicarMigracionNivel`
Expected: PASS (1 test). Después, **borrar el archivo** `tests/TrucoUruguayo.Bot.Tests/_AplicarMigracionNivel.cs` — es descartable, no se commitea.

- [ ] **Step 2: Escribir los tests que fallan**

Crear `tests/TrucoUruguayo.Bot.Tests/Modelo/NivelCalculadoraTests.cs`:

```csharp
using TrucoUruguayo.Bot.Modelo;
using Xunit;

namespace TrucoUruguayo.Bot.Tests.Modelo;

public class NivelCalculadoraTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 100)]
    [InlineData(3, 300)]
    [InlineData(4, 600)]
    public void XpParaAlcanzarNivel_DevuelveElUmbralAcumulado(int nivel, int esperado)
    {
        Assert.Equal(esperado, NivelCalculadora.XpParaAlcanzarNivel(nivel));
    }
}
```

En `tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs`, reemplazar el test existente:

```csharp
    [Fact]
    public async Task RegistrarUsuarioAsync_UsuarioNuevo_QuedaConMilMonedasYSePuedeConsultar()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        var id = UsuarioDePrueba.GenerarId();

        await using var usuario = UsuarioDePrueba.ParaLimpieza(_connectionString, id);

        var registrado = await repositorio.RegistrarUsuarioAsync(id, "prueba");
        var consultado = await repositorio.ObtenerUsuarioAsync(id);

        Assert.Equal(1000, registrado.Monedas);
        Assert.NotNull(consultado);
        Assert.Equal(1000, consultado!.Monedas);
        Assert.Equal("prueba", consultado.Nombre);
    }
```

por:

```csharp
    [Fact]
    public async Task RegistrarUsuarioAsync_UsuarioNuevo_QuedaConMilMonedasYSePuedeConsultar()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        var id = UsuarioDePrueba.GenerarId();

        await using var usuario = UsuarioDePrueba.ParaLimpieza(_connectionString, id);

        var registrado = await repositorio.RegistrarUsuarioAsync(id, "prueba");
        var consultado = await repositorio.ObtenerUsuarioAsync(id);

        Assert.Equal(1000, registrado.Monedas);
        Assert.Equal(1, registrado.Nivel);
        Assert.NotNull(consultado);
        Assert.Equal(1000, consultado!.Monedas);
        Assert.Equal("prueba", consultado.Nombre);
        Assert.Equal(1, consultado.Nivel);
    }
```

- [ ] **Step 3: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter "FullyQualifiedName~NivelCalculadoraTests|FullyQualifiedName~RegistrarUsuarioAsync_UsuarioNuevo_QuedaConMilMonedasYSePuedeConsultar"`
Expected: FAIL — error de compilación (`NivelCalculadora` no existe, `Usuario` no tiene `Nivel`).

- [ ] **Step 4: Implementar**

En `src/TrucoUruguayo.Bot/Datos/schema.sql`, reemplazar:

```sql
CREATE TABLE IF NOT EXISTS usuarios (
    id BIGINT PRIMARY KEY,
    nombre TEXT NOT NULL,
    monedas INTEGER NOT NULL DEFAULT 0,
    victorias INTEGER NOT NULL DEFAULT 0,
    derrotas INTEGER NOT NULL DEFAULT 0,
    xp INTEGER NOT NULL DEFAULT 0
);
```

por:

```sql
CREATE TABLE IF NOT EXISTS usuarios (
    id BIGINT PRIMARY KEY,
    nombre TEXT NOT NULL,
    monedas INTEGER NOT NULL DEFAULT 0,
    victorias INTEGER NOT NULL DEFAULT 0,
    derrotas INTEGER NOT NULL DEFAULT 0,
    xp INTEGER NOT NULL DEFAULT 0,
    nivel INTEGER NOT NULL DEFAULT 1
);

-- No hay runner de migraciones: este ALTER idempotente hace que volver a correr
-- schema.sql contra una base ya existente agregue la columna sin romper nada.
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS nivel INTEGER NOT NULL DEFAULT 1;
```

Crear `src/TrucoUruguayo.Bot/Modelo/NivelCalculadora.cs`:

```csharp
namespace TrucoUruguayo.Bot.Modelo;

public static class NivelCalculadora
{
    public static int XpParaAlcanzarNivel(int nivel) => 100 * (nivel - 1) * nivel / 2;
}
```

En `src/TrucoUruguayo.Bot/Modelo/Usuario.cs`, reemplazar:

```csharp
namespace TrucoUruguayo.Bot.Modelo;

public class Usuario
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Monedas { get; set; }
    public int Victorias { get; set; }
    public int Derrotas { get; set; }
    public int Xp { get; set; }
}
```

por:

```csharp
namespace TrucoUruguayo.Bot.Modelo;

public class Usuario
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Monedas { get; set; }
    public int Victorias { get; set; }
    public int Derrotas { get; set; }
    public int Xp { get; set; }
    public int Nivel { get; set; }
}
```

En `src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs`, reemplazar `ObtenerUsuarioAsync` y `RegistrarUsuarioAsync`:

```csharp
    public async Task<Usuario?> ObtenerUsuarioAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp
            FROM usuarios
            WHERE id = @Id
            """;

        return await conexion.QuerySingleOrDefaultAsync<Usuario>(sql, new { Id = (long)discordId });
    }

    public async Task<Usuario> RegistrarUsuarioAsync(ulong discordId, string nombre)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            INSERT INTO usuarios (id, nombre, monedas, victorias, derrotas, xp)
            VALUES (@Id, @Nombre, @Monedas, 0, 0, 0)
            RETURNING id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp
            """;

        return await conexion.QuerySingleAsync<Usuario>(
            sql,
            new { Id = (long)discordId, Nombre = nombre, Monedas = MonedasIniciales });
    }
```

por:

```csharp
    public async Task<Usuario?> ObtenerUsuarioAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel
            FROM usuarios
            WHERE id = @Id
            """;

        return await conexion.QuerySingleOrDefaultAsync<Usuario>(sql, new { Id = (long)discordId });
    }

    public async Task<Usuario> RegistrarUsuarioAsync(ulong discordId, string nombre)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            INSERT INTO usuarios (id, nombre, monedas, victorias, derrotas, xp)
            VALUES (@Id, @Nombre, @Monedas, 0, 0, 0)
            RETURNING id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel
            """;

        return await conexion.QuerySingleAsync<Usuario>(
            sql,
            new { Id = (long)discordId, Nombre = nombre, Monedas = MonedasIniciales });
    }
```

Y reemplazar el `SELECT` de `ObtenerTopUsuariosAsync`:

```csharp
        var sql = $"""
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp
            FROM usuarios
            ORDER BY {columna} DESC
            LIMIT @Limite
            """;
```

por:

```csharp
        var sql = $"""
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel
            FROM usuarios
            ORDER BY {columna} DESC
            LIMIT @Limite
            """;
```

- [ ] **Step 5: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: PASS (toda la suite, incluidos los tests nuevos/modificados).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Bot/Datos/schema.sql src/TrucoUruguayo.Bot/Modelo/Usuario.cs src/TrucoUruguayo.Bot/Modelo/NivelCalculadora.cs src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs tests/TrucoUruguayo.Bot.Tests/Modelo/NivelCalculadoraTests.cs tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs
git commit -m "$(cat <<'EOF'
feat: agrega columna nivel y NivelCalculadora

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `UsuarioRepository.SumarExpAsync`

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs`
- Test: `tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs`

**Interfaces:**
- Consumes (de Task 1): `NivelCalculadora.XpParaAlcanzarNivel(int) -> int`, `Usuario.Xp`, `Usuario.Nivel`.
- Produces (usado por Task 4): `UsuarioRepository.SumarExpAsync(ulong discordId, int cantidadExp) -> Task<(bool SubioDeNivel, int NuevoNivel)>`.

- [ ] **Step 1: Escribir los tests que fallan**

Agregar en `tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs`, después del test `ObtenerUsuarioAsync_UsuarioInexistente_DevuelveNull`:

```csharp
    [Fact]
    public async Task SumarExpAsync_SinCruzarUmbral_NoSubeDeNivel()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var (subioDeNivel, nuevoNivel) = await repositorio.SumarExpAsync(usuario.Id, 50);

        Assert.False(subioDeNivel);
        Assert.Equal(1, nuevoNivel);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(50, actualizado!.Xp);
        Assert.Equal(1, actualizado.Nivel);
    }

    [Fact]
    public async Task SumarExpAsync_CruzaUnUmbral_SubeDeNivel()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var (subioDeNivel, nuevoNivel) = await repositorio.SumarExpAsync(usuario.Id, 150);

        Assert.True(subioDeNivel);
        Assert.Equal(2, nuevoNivel);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(150, actualizado!.Xp);
        Assert.Equal(2, actualizado.Nivel);
    }

    [Fact]
    public async Task SumarExpAsync_CruzaVariosUmbrales_SubeVariosNiveles()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        var (subioDeNivel, nuevoNivel) = await repositorio.SumarExpAsync(usuario.Id, 500);

        Assert.True(subioDeNivel);
        Assert.Equal(3, nuevoNivel);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(500, actualizado!.Xp);
        Assert.Equal(3, actualizado.Nivel);
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter SumarExpAsync`
Expected: FAIL — error de compilación (`UsuarioRepository` no tiene `SumarExpAsync`).

- [ ] **Step 3: Implementar `SumarExpAsync`**

En `src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs`, agregar esta clase privada justo antes del cierre de la clase `UsuarioRepository` (después del último método, `ObtenerHistorialAsync`):

```csharp
    public async Task<(bool SubioDeNivel, int NuevoNivel)> SumarExpAsync(ulong discordId, int cantidadExp)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);
        await conexion.OpenAsync();
        await using var transaccion = await conexion.BeginTransactionAsync();

        var id = (long)discordId;

        const string sqlSeleccionar = """
            SELECT xp AS Xp, nivel AS Nivel
            FROM usuarios
            WHERE id = @Id
            FOR UPDATE
            """;

        var actual = await conexion.QuerySingleAsync<XpYNivel>(
            new CommandDefinition(sqlSeleccionar, new { Id = id }, transaccion));

        var nuevoXp = actual.Xp + cantidadExp;
        var nuevoNivel = actual.Nivel;

        while (nuevoXp >= NivelCalculadora.XpParaAlcanzarNivel(nuevoNivel + 1))
        {
            nuevoNivel++;
        }

        const string sqlActualizar = "UPDATE usuarios SET xp = @Xp, nivel = @Nivel WHERE id = @Id";

        await conexion.ExecuteAsync(
            new CommandDefinition(sqlActualizar, new { Xp = nuevoXp, Nivel = nuevoNivel, Id = id }, transaccion));

        await transaccion.CommitAsync();

        return (nuevoNivel > actual.Nivel, nuevoNivel);
    }

    private sealed class XpYNivel
    {
        public int Xp { get; set; }
        public int Nivel { get; set; }
    }
```

Agregar el using que falta al principio del archivo (`TrucoUruguayo.Bot.Modelo` para `NivelCalculadora`):

```csharp
using Dapper;
using Npgsql;
using TrucoUruguayo.Bot.Modelo;
```

- [ ] **Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter SumarExpAsync`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs
git commit -m "$(cat <<'EOF'
feat: agrega SumarExpAsync con calculo de nivel

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Sacar el XP de `SumarVictoriaAsync`/`SumarDerrotaAsync`

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs:55-71`
- Test: `tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs:52-76,111-129`

**Interfaces:**
- Consumes (de Task 2): `UsuarioRepository.SumarExpAsync(ulong, int)`.
- Produces: sin cambios de firma — `SumarVictoriaAsync`/`SumarDerrotaAsync` siguen devolviendo `Task`, solo cambia qué columnas tocan.

- [ ] **Step 1: Actualizar los tests que van a fallar con el código actual**

En `tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs`, reemplazar:

```csharp
    [Fact]
    public async Task SumarVictoriaAsync_SumaUnaVictoriaYQuinceXp()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarVictoriaAsync(usuario.Id);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(1, actualizado!.Victorias);
        Assert.Equal(15, actualizado.Xp);
    }

    [Fact]
    public async Task SumarDerrotaAsync_SumaUnaDerrotaYTresXp()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarDerrotaAsync(usuario.Id);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(1, actualizado!.Derrotas);
        Assert.Equal(3, actualizado.Xp);
    }
```

por:

```csharp
    [Fact]
    public async Task SumarVictoriaAsync_SumaUnaVictoriaYNoTocaElXp()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarVictoriaAsync(usuario.Id);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(1, actualizado!.Victorias);
        Assert.Equal(0, actualizado.Xp);
    }

    [Fact]
    public async Task SumarDerrotaAsync_SumaUnaDerrotaYNoTocaElXp()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarDerrotaAsync(usuario.Id);

        var actualizado = await repositorio.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(1, actualizado!.Derrotas);
        Assert.Equal(0, actualizado.Xp);
    }
```

Y reemplazar:

```csharp
    [Fact]
    public async Task ObtenerTopUsuariosAsync_OrdenXp_OrdenaPorXpDescendente()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuarioBajo = await UsuarioDePrueba.CrearAsync(_connectionString);
        await using var usuarioAlto = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarVictoriaAsync(usuarioBajo.Id);
        for (var i = 0; i < 5; i++)
        {
            await repositorio.SumarVictoriaAsync(usuarioAlto.Id);
        }

        var top = (await repositorio.ObtenerTopUsuariosAsync("xp", limite: 1000)).ToList();
        var posicionAlto = top.FindIndex(u => (ulong)u.Id == usuarioAlto.Id);
        var posicionBajo = top.FindIndex(u => (ulong)u.Id == usuarioBajo.Id);

        Assert.True(posicionAlto < posicionBajo);
    }
```

por:

```csharp
    [Fact]
    public async Task ObtenerTopUsuariosAsync_OrdenXp_OrdenaPorXpDescendente()
    {
        var repositorio = new UsuarioRepository(_connectionString);
        await using var usuarioBajo = await UsuarioDePrueba.CrearAsync(_connectionString);
        await using var usuarioAlto = await UsuarioDePrueba.CrearAsync(_connectionString);

        await repositorio.SumarExpAsync(usuarioBajo.Id, 50);
        await repositorio.SumarExpAsync(usuarioAlto.Id, 250);

        var top = (await repositorio.ObtenerTopUsuariosAsync("xp", limite: 1000)).ToList();
        var posicionAlto = top.FindIndex(u => (ulong)u.Id == usuarioAlto.Id);
        var posicionBajo = top.FindIndex(u => (ulong)u.Id == usuarioBajo.Id);

        Assert.True(posicionAlto < posicionBajo);
    }
```

- [ ] **Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj --filter "FullyQualifiedName~SumarVictoriaAsync_SumaUnaVictoriaYNoTocaElXp|FullyQualifiedName~SumarDerrotaAsync_SumaUnaDerrotaYNoTocaElXp"`
Expected: FAIL — `actualizado.Xp` es 15/3, no 0 (el código viejo todavía suma XP en esos métodos).

- [ ] **Step 3: Implementar**

En `src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs`, reemplazar:

```csharp
    public async Task SumarVictoriaAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = "UPDATE usuarios SET victorias = victorias + 1, xp = xp + 15 WHERE id = @Id";

        await conexion.ExecuteAsync(sql, new { Id = (long)discordId });
    }

    public async Task SumarDerrotaAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = "UPDATE usuarios SET derrotas = derrotas + 1, xp = xp + 3 WHERE id = @Id";

        await conexion.ExecuteAsync(sql, new { Id = (long)discordId });
    }
```

por:

```csharp
    public async Task SumarVictoriaAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = "UPDATE usuarios SET victorias = victorias + 1 WHERE id = @Id";

        await conexion.ExecuteAsync(sql, new { Id = (long)discordId });
    }

    public async Task SumarDerrotaAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = "UPDATE usuarios SET derrotas = derrotas + 1 WHERE id = @Id";

        await conexion.ExecuteAsync(sql, new { Id = (long)discordId });
    }
```

- [ ] **Step 4: Correr toda la suite y verificar que pasa**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: PASS (toda la suite).

- [ ] **Step 5: Commit**

```bash
git add src/TrucoUruguayo.Bot/Datos/UsuarioRepository.cs tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs
git commit -m "$(cat <<'EOF'
refactor: saca el bump de xp de SumarVictoriaAsync/SumarDerrotaAsync

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Reparto de XP y aviso de nivel al terminar una partida

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Servicios/GestorPartidas.cs:68-102` (`ChequearInactividadAsync`)
- Modify: `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs:483-499` (`FinalizarYAnunciarRonda`)

**Interfaces:**
- Consumes (de Task 2): `UsuarioRepository.SumarExpAsync(ulong discordId, int cantidadExp) -> Task<(bool SubioDeNivel, int NuevoNivel)>`.
- Produces: nada nuevo consumido por otras tasks.

No hay tests automatizados para estos dos métodos (son handlers/callbacks de Discord sin cobertura hoy, igual que el resto de esos flujos). Se verifica con `dotnet build` + la suite completa (no debe romper nada existente) + una prueba manual.

- [ ] **Step 1: Reparto de XP en `GestorPartidas.ChequearInactividadAsync`**

En `src/TrucoUruguayo.Bot/Servicios/GestorPartidas.cs`, reemplazar:

```csharp
                await _usuarioRepository.ActualizarMonedasAsync(ganadorId, pozo);
                await _usuarioRepository.RegistrarPartidaAsync(ganadorId, afkId, apuesta);
                await _usuarioRepository.SumarVictoriaAsync(ganadorId);
                await _usuarioRepository.SumarDerrotaAsync(afkId);

                FinalizarPartida(canalId);

                if (_client.GetChannel(canalId) is IMessageChannel canal)
                {
                    await canal.SendMessageAsync(
                        $"⏳ ¡<@{afkId}> se quedó dormido (AFK)! <@{ganadorId}> gana por abandono y se lleva el pozo.");
                }
```

por:

```csharp
                await _usuarioRepository.ActualizarMonedasAsync(ganadorId, pozo);
                await _usuarioRepository.RegistrarPartidaAsync(ganadorId, afkId, apuesta);
                await _usuarioRepository.SumarVictoriaAsync(ganadorId);
                await _usuarioRepository.SumarDerrotaAsync(afkId);

                var (subioGanador, nivelGanador) = await _usuarioRepository.SumarExpAsync(ganadorId, 50);
                var (subioPerdedor, nivelPerdedor) = await _usuarioRepository.SumarExpAsync(afkId, 15);

                FinalizarPartida(canalId);

                if (_client.GetChannel(canalId) is IMessageChannel canal)
                {
                    await canal.SendMessageAsync(
                        $"⏳ ¡<@{afkId}> se quedó dormido (AFK)! <@{ganadorId}> gana por abandono y se lleva el pozo.");

                    if (subioGanador)
                    {
                        await canal.SendMessageAsync($"🎉 ¡Felicidades <@{ganadorId}>! Has alcanzado el **Nivel {nivelGanador}**.");
                    }

                    if (subioPerdedor)
                    {
                        await canal.SendMessageAsync($"🎉 ¡Felicidades <@{afkId}>! Has alcanzado el **Nivel {nivelPerdedor}**.");
                    }
                }
```

- [ ] **Step 2: Reparto de XP en `TrucoModule.FinalizarYAnunciarRonda`**

En `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`, reemplazar:

```csharp
    private async Task FinalizarYAnunciarRonda(Ronda ronda)
    {
        var ganadorId = ronda.GanadorRonda!.Value;
        var perdedorId = ganadorId == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;
        var apuesta = _gestorPartidas.ApuestasActivas.GetValueOrDefault(Context.Channel.Id);
        var pozo = apuesta * 2;

        await _usuarioRepository.ActualizarMonedasAsync(ganadorId, pozo);
        await _usuarioRepository.RegistrarPartidaAsync(ganadorId, perdedorId, apuesta);
        await _usuarioRepository.SumarVictoriaAsync(ganadorId);
        await _usuarioRepository.SumarDerrotaAsync(perdedorId);

        _gestorPartidas.FinalizarPartida(Context.Channel.Id);

        await Context.Channel.SendMessageAsync(
            $"🏆 ¡Ronda finalizada! <@{ganadorId}> gana la partida y se lleva 🪙 {pozo} monedas!");
    }
```

por:

```csharp
    private async Task FinalizarYAnunciarRonda(Ronda ronda)
    {
        var ganadorId = ronda.GanadorRonda!.Value;
        var perdedorId = ganadorId == ronda.Jugador1Id ? ronda.Jugador2Id : ronda.Jugador1Id;
        var apuesta = _gestorPartidas.ApuestasActivas.GetValueOrDefault(Context.Channel.Id);
        var pozo = apuesta * 2;

        await _usuarioRepository.ActualizarMonedasAsync(ganadorId, pozo);
        await _usuarioRepository.RegistrarPartidaAsync(ganadorId, perdedorId, apuesta);
        await _usuarioRepository.SumarVictoriaAsync(ganadorId);
        await _usuarioRepository.SumarDerrotaAsync(perdedorId);

        var (subioGanador, nivelGanador) = await _usuarioRepository.SumarExpAsync(ganadorId, 50);
        var (subioPerdedor, nivelPerdedor) = await _usuarioRepository.SumarExpAsync(perdedorId, 15);

        _gestorPartidas.FinalizarPartida(Context.Channel.Id);

        await Context.Channel.SendMessageAsync(
            $"🏆 ¡Ronda finalizada! <@{ganadorId}> gana la partida y se lleva 🪙 {pozo} monedas!");

        if (subioGanador)
        {
            await Context.Channel.SendMessageAsync($"🎉 ¡Felicidades <@{ganadorId}>! Has alcanzado el **Nivel {nivelGanador}**.");
        }

        if (subioPerdedor)
        {
            await Context.Channel.SendMessageAsync($"🎉 ¡Felicidades <@{perdedorId}>! Has alcanzado el **Nivel {nivelPerdedor}**.");
        }
    }
```

- [ ] **Step 3: Compilar y correr toda la suite**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: build sin errores, toda la suite en verde (no se rompió nada existente).

- [ ] **Step 4: Prueba manual**

Con el bot corriendo: jugar (o forzar por AFK) una partida completa y confirmar que, además del mensaje de fin de partida de siempre, aparece `🎉 ¡Felicidades @ganador! Has alcanzado el Nivel N.` cuando corresponda (ej. arrancando una cuenta nueva, que empieza en Nivel 1 con 0 XP, un solo `+50` no alcanza para subir — hacen falta 2 victorias para pasar los 100 XP del nivel 2).

- [ ] **Step 5: Commit**

```bash
git add src/TrucoUruguayo.Bot/Servicios/GestorPartidas.cs src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs
git commit -m "$(cat <<'EOF'
feat: reparte xp y avisa subidas de nivel al terminar una partida

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Barra de progreso en `/perfil`

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Modulos/PerfilModule.cs`

**Interfaces:**
- Consumes (de Task 1): `NivelCalculadora.XpParaAlcanzarNivel(int) -> int`, `Usuario.Xp`, `Usuario.Nivel`.
- Produces: nada nuevo consumido por otras tasks.

Sin tests automatizados (handler de Discord, mismo criterio que el resto de `PerfilModule`). Se verifica con `dotnet build` + suite completa + prueba manual.

- [ ] **Step 1: Agregar el using y el helper `GenerarBarraExp`**

En `src/TrucoUruguayo.Bot/Modulos/PerfilModule.cs`, reemplazar:

```csharp
using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;
```

por:

```csharp
using Discord;
using Discord.Interactions;
using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Modelo;
```

- [ ] **Step 2: Calcular XP del nivel actual y reemplazar el field de `/perfil`**

Reemplazar:

```csharp
        var embed = new EmbedBuilder()
            .WithTitle($"👤 {usuarioDb.Nombre}")
            .AddField("🪙 Monedas", usuarioDb.Monedas, true)
            .AddField("✅ Victorias", usuarioDb.Victorias, true)
            .AddField("❌ Derrotas", usuarioDb.Derrotas, true)
            .AddField("✨ XP", usuarioDb.Xp, true)
            .WithColor(Color.Gold);
```

por:

```csharp
        var xpNivelActual = usuarioDb.Xp - NivelCalculadora.XpParaAlcanzarNivel(usuarioDb.Nivel);
        var xpNecesaria = usuarioDb.Nivel * 100;

        var embed = new EmbedBuilder()
            .WithTitle($"👤 {usuarioDb.Nombre}")
            .AddField("🪙 Monedas", usuarioDb.Monedas, true)
            .AddField("✅ Victorias", usuarioDb.Victorias, true)
            .AddField("❌ Derrotas", usuarioDb.Derrotas, true)
            .AddField("🎮 Nivel y Experiencia", $"**Nivel {usuarioDb.Nivel}**\n{GenerarBarraExp(xpNivelActual, xpNecesaria)}")
            .WithColor(Color.Gold);
```

- [ ] **Step 3: Agregar el helper privado `GenerarBarraExp`**

Al final de la clase `PerfilModule`, justo antes del último `}` de cierre de la clase, agregar:

```csharp

    private string GenerarBarraExp(int expActual, int expNecesaria, int longitudBarra = 10)
    {
        double porcentaje = Math.Clamp((double)expActual / expNecesaria, 0, 1);
        int bloquesLlenos = (int)Math.Round(porcentaje * longitudBarra);
        int bloquesVacios = longitudBarra - bloquesLlenos;
        return $"[{new string('█', bloquesLlenos)}{new string('░', bloquesVacios)}] {expActual}/{expNecesaria} XP";
    }
```

- [ ] **Step 4: Compilar y correr toda la suite**

Run: `dotnet build src/TrucoUruguayo.Bot/TrucoUruguayo.Bot.csproj && dotnet test tests/TrucoUruguayo.Bot.Tests/TrucoUruguayo.Bot.Tests.csproj`
Expected: build sin errores, toda la suite en verde.

- [ ] **Step 5: Prueba manual**

Con el bot corriendo, correr `/perfil` y confirmar que aparece el field "🎮 Nivel y Experiencia" con el nivel y una barra tipo `[████░░░░░░] 40/100 XP`, y que el field viejo "✨ XP" ya no está.

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/PerfilModule.cs
git commit -m "$(cat <<'EOF'
feat: agrega barra de progreso de nivel a /perfil

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
