# Expiración, límite y cancelación de retos pendientes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un reto de `/truco` deja de ser eterno: expira solo a los 30s si no se acepta, un retador no puede tener más de un reto saliente a la vez, y el propio retador puede cancelarlo.

**Architecture:** `GestorPartidas` gana un `ConcurrentDictionary<ulong, RetoPendiente>` (clave: `RetadorId`) con un `Timer` de un solo disparo por reto para la expiración automática. `TrucoModule` consulta y actualiza ese estado en `/truco`, en el botón "✅ Aceptar" existente y en un botón nuevo "❌ Cancelar".

**Tech Stack:** C# / .NET 10, Discord.Net.Interactions 3.20.1, xUnit.

## Global Constraints

- El límite de expiración es 30 segundos (spec: "a los 30 segundos que se cancele el reto si el otro no acepta").
- Un retador solo puede tener un reto pendiente a la vez (spec: "ni poder retar a más gente").
- Solo el propio retador puede cancelar su reto (spec: "que se pueda cancelar la apuesta también siendo uno mismo el que reta"). No se agrega botón de rechazo para el retado (fuera de alcance, ver spec).
- Al expirar o cancelarse, se edita el mismo mensaje original del desafío (no se manda uno nuevo) y se vacían sus componentes.
- Seguir las convenciones existentes del repo: nombres en español (`RetoPendiente`, `RetosPendientes`, `RegistrarReto`), clases con constructor + propiedades `{ get; }` (no `required`/init, ver `Ronda.cs`), tests xUnit en `tests/TrucoUruguayo.Bot.Tests/Servicios/GestorPartidasTests.cs` usando el helper `CrearGestor()`.

Spec completo: `docs/superpowers/specs/2026-09-15-reto-expiracion-design.md`

---

### Task 1: `RetoPendiente` + estado de retos pendientes en `GestorPartidas`

**Files:**
- Create: `src/TrucoUruguayo.Bot/Servicios/RetoPendiente.cs`
- Modify: `src/TrucoUruguayo.Bot/Servicios/GestorPartidas.cs`
- Test: `tests/TrucoUruguayo.Bot.Tests/Servicios/GestorPartidasTests.cs`

**Interfaces:**
- Consumes: nada nuevo (usa `System.Threading.Timer`, ya disponible por implicit usings, igual que el resto de `GestorPartidas.cs`).
- Produces (usado por Task 2):
  - `RetoPendiente(ulong retadorId, ulong retadoId, int apuesta, int puntos, ulong canalId, ulong mensajeId)` — constructor.
  - `RetoPendiente.RetadorId/RetadoId/Apuesta/Puntos/CanalId/MensajeId` — propiedades `ulong`/`int` de solo lectura.
  - `GestorPartidas.RetosPendientes` — `ConcurrentDictionary<ulong, RetoPendiente>` público (clave: `RetadorId`).
  - `bool GestorPartidas.TieneRetoPendiente(ulong retadorId)`.
  - `bool GestorPartidas.RegistrarReto(RetoPendiente reto)` — `true` si se registró, `false` si el retador ya tenía uno pendiente.
  - `bool GestorPartidas.TryQuitarReto(ulong retadorId, out RetoPendiente? reto)` — remueve y descarta el timer; `true` si había uno.
  - Constructor `GestorPartidas(DiscordSocketClient client, UsuarioRepository usuarioRepository, TimeSpan? limiteReto = null)` — nuevo parámetro opcional (por defecto 30s) para poder testear la expiración sin esperar 30s reales.

- [ ] **Step 1: Crear `RetoPendiente.cs`**

```csharp
namespace TrucoUruguayo.Bot.Servicios;

public class RetoPendiente
{
    public ulong RetadorId { get; }
    public ulong RetadoId { get; }
    public int Apuesta { get; }
    public int Puntos { get; }
    public ulong CanalId { get; }
    public ulong MensajeId { get; }
    public Timer? TimerExpiracion { get; set; }

    public RetoPendiente(ulong retadorId, ulong retadoId, int apuesta, int puntos, ulong canalId, ulong mensajeId)
    {
        RetadorId = retadorId;
        RetadoId = retadoId;
        Apuesta = apuesta;
        Puntos = puntos;
        CanalId = canalId;
        MensajeId = mensajeId;
    }
}
```

- [ ] **Step 2: Escribir los tests que fallan en `GestorPartidasTests.cs`**

Primero actualizar el helper `CrearGestor` para aceptar un `limiteReto` opcional (los tests existentes que llaman `CrearGestor()` sin argumentos siguen funcionando igual):

```csharp
private static GestorPartidas CrearGestor(TimeSpan? limiteReto = null) =>
    new(new DiscordSocketClient(), new UsuarioRepository("Host=localhost"), limiteReto);
```

Agregar estos `[Fact]` al final de la clase, antes de la última `}`:

```csharp
[Fact]
public void RegistrarReto_SinRetoPrevio_LoRegistraYDevuelveTrue()
{
    var gestor = CrearGestor();
    var reto = new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999);

    var resultado = gestor.RegistrarReto(reto);

    Assert.True(resultado);
    Assert.True(gestor.TieneRetoPendiente(10));
}

[Fact]
public void RegistrarReto_RetadorYaTieneRetoPendiente_NoLoRegistraYDevuelveFalse()
{
    var gestor = CrearGestor();
    gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999));

    var resultado = gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 30, apuesta: 50, puntos: 10, canalId: 2, mensajeId: 998));

    Assert.False(resultado);
    Assert.Equal(20UL, gestor.RetosPendientes[10].RetadoId);
}

[Fact]
public void TryQuitarReto_RetoExistente_LoRemueveYDevuelveTrue()
{
    var gestor = CrearGestor();
    gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999));

    var resultado = gestor.TryQuitarReto(10, out var reto);

    Assert.True(resultado);
    Assert.NotNull(reto);
    Assert.Equal(20UL, reto!.RetadoId);
    Assert.False(gestor.TieneRetoPendiente(10));
}

[Fact]
public void TryQuitarReto_SinRetoPendiente_DevuelveFalse()
{
    var gestor = CrearGestor();

    var resultado = gestor.TryQuitarReto(999, out var reto);

    Assert.False(resultado);
    Assert.Null(reto);
}

[Fact]
public void TryQuitarReto_DespuesDeQuitarSePuedeRegistrarUnoNuevo()
{
    var gestor = CrearGestor();
    gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999));
    gestor.TryQuitarReto(10, out _);

    var resultado = gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 30, apuesta: 50, puntos: 10, canalId: 2, mensajeId: 998));

    Assert.True(resultado);
    Assert.Equal(30UL, gestor.RetosPendientes[10].RetadoId);
}

[Fact]
public async Task RegistrarReto_LimiteRetoVencido_LoQuitaAutomaticamenteDeLosPendientes()
{
    var gestor = CrearGestor(limiteReto: TimeSpan.FromMilliseconds(50));
    gestor.RegistrarReto(new RetoPendiente(retadorId: 10, retadoId: 20, apuesta: 100, puntos: 15, canalId: 1, mensajeId: 999));

    await Task.Delay(TimeSpan.FromMilliseconds(300));

    Assert.False(gestor.TieneRetoPendiente(10));
}
```

- [ ] **Step 3: Correr los tests y verificar que fallan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests --filter GestorPartidasTests`
Expected: FAIL (error de compilación: `RetoPendiente` no tiene ese constructor accesible desde `GestorPartidas` en el estado actual / `TieneRetoPendiente`, `RegistrarReto`, `TryQuitarReto` y `RetosPendientes` no existen todavía en `GestorPartidas`, y el constructor de tres argumentos no existe).

- [ ] **Step 4: Implementar los cambios en `GestorPartidas.cs`**

Modificar los campos privados y el constructor (reemplazar desde `private readonly Timer _timerAfk;` hasta el cierre del constructor):

```csharp
    private readonly DiscordSocketClient _client;
    private readonly UsuarioRepository _usuarioRepository;
    private readonly Timer _timerAfk;
    private readonly TimeSpan _limiteReto;

    public ConcurrentDictionary<ulong, Ronda> PartidasActivas { get; } = new();
    public ConcurrentDictionary<ulong, ulong> JugadoresActivos { get; } = new();
    public ConcurrentDictionary<ulong, int> ApuestasActivas { get; } = new();
    public ConcurrentDictionary<ulong, RetoPendiente> RetosPendientes { get; } = new();

    public GestorPartidas(DiscordSocketClient client, UsuarioRepository usuarioRepository, TimeSpan? limiteReto = null)
    {
        _client = client;
        _usuarioRepository = usuarioRepository;
        _limiteReto = limiteReto ?? TimeSpan.FromSeconds(30);
        _timerAfk = new Timer(ChequearInactividadAsync, null, IntervaloChequeoAfk, IntervaloChequeoAfk);
    }
```

Agregar estos métodos nuevos justo después de `FinalizarPartida` (antes de `ChequearInactividadAsync`):

```csharp
    public bool TieneRetoPendiente(ulong retadorId) => RetosPendientes.ContainsKey(retadorId);

    public bool RegistrarReto(RetoPendiente reto)
    {
        if (!RetosPendientes.TryAdd(reto.RetadorId, reto))
        {
            return false;
        }

        reto.TimerExpiracion = new Timer(ExpirarRetoAsync, reto.RetadorId, _limiteReto, Timeout.InfiniteTimeSpan);
        return true;
    }

    public bool TryQuitarReto(ulong retadorId, out RetoPendiente? reto)
    {
        if (!RetosPendientes.TryRemove(retadorId, out reto))
        {
            return false;
        }

        reto.TimerExpiracion?.Dispose();
        return true;
    }

    private async void ExpirarRetoAsync(object? state)
    {
        var retadorId = (ulong)state!;

        if (!TryQuitarReto(retadorId, out var reto) || reto is null)
        {
            return;
        }

        try
        {
            if (_client.GetChannel(reto.CanalId) is IMessageChannel canal)
            {
                await canal.ModifyMessageAsync(reto.MensajeId, mensaje =>
                {
                    mensaje.Content = $"⌛ El reto de <@{reto.RetadorId}> a <@{reto.RetadoId}> expiró (no se aceptó a tiempo).";
                    mensaje.Components = new ComponentBuilder().Build();
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error expirando reto: {ex.Message}");
        }
    }
```

- [ ] **Step 5: Correr los tests y verificar que pasan**

Run: `dotnet test tests/TrucoUruguayo.Bot.Tests --filter GestorPartidasTests`
Expected: PASS (todos los tests de `GestorPartidasTests`, incluidos los nuevos).

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Bot/Servicios/RetoPendiente.cs src/TrucoUruguayo.Bot/Servicios/GestorPartidas.cs tests/TrucoUruguayo.Bot.Tests/Servicios/GestorPartidasTests.cs
git commit -m "$(cat <<'EOF'
feat: agrega expiracion y limite de un reto pendiente por retador

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Bloquear, cancelar y expirar el reto desde `TrucoModule`

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs:24-125`

**Interfaces:**
- Consumes (de Task 1): `RetoPendiente`, `_gestorPartidas.TieneRetoPendiente(ulong)`, `_gestorPartidas.RegistrarReto(RetoPendiente)`, `_gestorPartidas.TryQuitarReto(ulong, out RetoPendiente?)`.
- Produces: nuevo componente de Discord `reto_cancelar_*_*_*_*` → `CancelarReto(ulong retadorId, ulong retadoId, int apuesta, int puntos)`.

No hay tests automatizados para los handlers de interacción de Discord en este repo (los `[SlashCommand]`/`[ComponentInteraction]` no están cubiertos hoy, ver `tests/TrucoUruguayo.Bot.Tests`); se verifica con `dotnet build` y una prueba manual del bot.

- [ ] **Step 1: Bloquear un segundo reto saliente en `TrucoAsync`**

En `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`, reemplazar este bloque (líneas 52-56):

```csharp
        if (_gestorPartidas.ObtenerPartidaPorUsuario(retadorId) is not null)
        {
            await RespondAsync("⚠️ Ya estás jugando una partida.", ephemeral: true);
            return;
        }
```

por:

```csharp
        if (_gestorPartidas.ObtenerPartidaPorUsuario(retadorId) is not null)
        {
            await RespondAsync("⚠️ Ya estás jugando una partida.", ephemeral: true);
            return;
        }

        if (_gestorPartidas.TieneRetoPendiente(retadorId))
        {
            await RespondAsync("⚠️ Ya tenés un reto pendiente. Cancelalo o esperá a que se resuelva.", ephemeral: true);
            return;
        }
```

- [ ] **Step 2: Agregar el botón "❌ Cancelar" y registrar el reto pendiente**

Reemplazar el bloque final de `TrucoAsync` (líneas 85-92):

```csharp
        var componentes = new ComponentBuilder()
            .WithButton("✅ Aceptar", $"reto_aceptar_{retadorId}_{retadoId}_{apuesta}_{puntos}", ButtonStyle.Success)
            .Build();

        await RespondAsync(
            $"⚔️ {usuario.Mention}, {Context.User.Mention} te desafía a una partida de Truco a {puntos} puntos por 🪙 {apuesta} monedas!",
            components: componentes);
    }
```

por:

```csharp
        var componentes = new ComponentBuilder()
            .WithButton("✅ Aceptar", $"reto_aceptar_{retadorId}_{retadoId}_{apuesta}_{puntos}", ButtonStyle.Success)
            .WithButton("❌ Cancelar", $"reto_cancelar_{retadorId}_{retadoId}_{apuesta}_{puntos}", ButtonStyle.Danger)
            .Build();

        await RespondAsync(
            $"⚔️ {usuario.Mention}, {Context.User.Mention} te desafía a una partida de Truco a {puntos} puntos por 🪙 {apuesta} monedas! (expira en 30s)",
            components: componentes);

        var mensaje = await GetOriginalResponseAsync();
        _gestorPartidas.RegistrarReto(new RetoPendiente(retadorId, retadoId, apuesta, puntos, Context.Channel.Id, mensaje.Id));
    }
```

- [ ] **Step 3: Validar el reto en `AceptarReto` contra el estado en memoria**

Reemplazar el inicio de `AceptarReto` (líneas 94-108):

```csharp
    [ComponentInteraction("reto_aceptar_*_*_*_*")]
    public async Task AceptarReto(ulong retadorId, ulong retadoId, int apuesta, int puntos)
    {
        if (Context.User.Id != retadoId)
        {
            await RespondAsync("🚫 Solo el retado puede aceptar.", ephemeral: true);
            return;
        }

        if (_gestorPartidas.ObtenerPartidaPorUsuario(retadorId) is not null
            || _gestorPartidas.ObtenerPartidaPorUsuario(retadoId) is not null)
        {
            await RespondAsync("⚠️ Ya no se puede aceptar este reto.", ephemeral: true);
            return;
        }
```

por:

```csharp
    [ComponentInteraction("reto_aceptar_*_*_*_*")]
    public async Task AceptarReto(ulong retadorId, ulong retadoId, int apuesta, int puntos)
    {
        if (Context.User.Id != retadoId)
        {
            await RespondAsync("🚫 Solo el retado puede aceptar.", ephemeral: true);
            return;
        }

        if (!_gestorPartidas.TryQuitarReto(retadorId, out _))
        {
            await RespondAsync("⚠️ Este reto ya no está disponible.", ephemeral: true);
            return;
        }

        if (_gestorPartidas.ObtenerPartidaPorUsuario(retadorId) is not null
            || _gestorPartidas.ObtenerPartidaPorUsuario(retadoId) is not null)
        {
            await RespondAsync("⚠️ Ya no se puede aceptar este reto.", ephemeral: true);
            return;
        }
```

- [ ] **Step 4: Agregar el handler `CancelarReto`**

Insertar este método nuevo justo después del cierre de `AceptarReto` (después de la línea `125` original, antes de `[ComponentInteraction("ver_mano")]`):

```csharp
    [ComponentInteraction("reto_cancelar_*_*_*_*")]
    public async Task CancelarReto(ulong retadorId, ulong retadoId, int apuesta, int puntos)
    {
        if (Context.User.Id != retadorId)
        {
            await RespondAsync("🚫 Solo quien retó puede cancelar el reto.", ephemeral: true);
            return;
        }

        if (!_gestorPartidas.TryQuitarReto(retadorId, out _))
        {
            await RespondAsync("⚠️ Este reto ya no está disponible.", ephemeral: true);
            return;
        }

        await ((SocketMessageComponent)Context.Interaction).UpdateAsync(mensaje =>
        {
            mensaje.Content = $"❌ *Reto cancelado por <@{retadorId}>.*";
            mensaje.Components = new ComponentBuilder().Build();
        });
    }
```

- [ ] **Step 5: Compilar y correr toda la suite de tests**

Run: `dotnet build && dotnet test`
Expected: build sin errores, todos los tests (incluidos los de `GestorPartidasTests` de Task 1) en verde.

- [ ] **Step 6: Prueba manual**

Con el bot corriendo en un servidor de prueba:
1. `/truco @otro apuesta:10 puntos:10` → aparecen los botones "✅ Aceptar" y "❌ Cancelar".
2. Intentar `/truco @otra-persona ...` de nuevo desde el mismo usuario retador → debe responder "⚠️ Ya tenés un reto pendiente...".
3. Esperar 30s sin aceptar → el mensaje original se edita a "⌛ El reto de ... expiró..." y los botones desaparecen. Confirmar que después el retador puede volver a usar `/truco`.
4. Repetir el desafío y pulsar "❌ Cancelar" como el retador → el mensaje se edita a "❌ *Reto cancelado...*". Confirmar que el retado ya no puede aceptar.
5. Repetir el desafío y pulsar "❌ Cancelar" como un tercer usuario (ni retador ni retado) → debe responder ephemeral "🚫 Solo quien retó puede cancelar el reto." y el reto sigue activo.

- [ ] **Step 7: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs
git commit -m "$(cat <<'EOF'
feat: permite cancelar el reto y expira los que no se aceptan en 30s

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```
