# Expiración, límite y cancelación de retos pendientes

## Contexto

`/truco` (`TrucoModule.TrucoAsync`, `src/TrucoUruguayo.Bot/Modulos/TrucoModule.cs`) crea un desafío 1v1 con apuesta de monedas. Hoy el reto no tiene ningún estado en memoria: todos sus datos (`retadorId`, `retadoId`, `apuesta`, `puntos`) viven codificados en el `custom_id` del botón "✅ Aceptar" del mensaje de Discord. Consecuencias:

- El botón queda activo indefinidamente — el reto nunca expira.
- Un mismo retador puede desafiar a cuanta gente quiera en paralelo, sin límite.
- No existe forma de cancelar un reto ya enviado, ni para el retador ni para nadie.

Este spec agrega estado en memoria para los retos pendientes, con expiración a los 30 segundos, límite de un reto saliente por retador, y cancelación por parte del propio retador.

## Alcance

Incluye:
- Expiración automática a los 30s si el retado no acepta.
- Un retador no puede tener más de un reto pendiente a la vez.
- El retador puede cancelar su propio reto con un botón.

Fuera de alcance (no pedido):
- Botón de "rechazar" explícito para el retado (el timeout ya cubre ese caso).
- Límite a cuántos retos distintos puede recibir un mismo retado en paralelo (no cambia el comportamiento actual).
- Persistencia de retos pendientes entre reinicios del bot (igual que `PartidasActivas`, es estado en memoria).

## Diseño

### `RetoPendiente` (nueva clase, `src/TrucoUruguayo.Bot/Servicios/RetoPendiente.cs`)

Estado de un reto en curso:

```csharp
public class RetoPendiente
{
    public required ulong RetadorId { get; init; }
    public required ulong RetadoId { get; init; }
    public required int Apuesta { get; init; }
    public required int Puntos { get; init; }
    public required ulong CanalId { get; init; }
    public required ulong MensajeId { get; init; }
    public Timer? TimerExpiracion { get; set; }
}
```

### `GestorPartidas`

Nuevo estado:

```csharp
private static readonly TimeSpan LimiteReto = TimeSpan.FromSeconds(30);
public ConcurrentDictionary<ulong, RetoPendiente> RetosPendientes { get; } = new(); // clave: RetadorId
```

Un retador solo puede tener una entrada en `RetosPendientes` a la vez — esto resuelve el límite de "no poder retar a más gente" sin necesidad de un contador aparte.

Nuevos métodos:

- `bool TieneRetoPendiente(ulong retadorId)` — chequeo usado por `/truco` antes de crear un reto nuevo.
- `bool RegistrarReto(RetoPendiente reto)` — inserta con `TryAdd` (mismo patrón atómico que `IniciarPartida`); si tiene éxito, arranca `reto.TimerExpiracion` como timer de un solo disparo a los 30s apuntando a `ExpirarRetoAsync`. Devuelve `false` si el retador ya tenía un reto (carrera perdida).
- `bool TryQuitarReto(ulong retadorId, out RetoPendiente? reto)` — remueve de `RetosPendientes` y descarta (`Dispose`) el timer si existe. Usado tanto por "Aceptar" como por "Cancelar" para invalidar el reto de forma consistente; devuelve `false` si ya no estaba (expiró o fue removido por otra carrera).
- `private async void ExpirarRetoAsync(object? state)` (state = `retadorId`) — si `TryQuitarReto` tiene éxito, edita el mensaje original (vía `_client.GetChannel(canalId)` como `IMessageChannel`, igual que ya hace `ChequearInactividadAsync`) reemplazando el contenido por el estado final y vaciando los componentes.

### `TrucoModule.cs`

- `TrucoAsync`: agrega, junto a los chequeos existentes, `if (_gestorPartidas.TieneRetoPendiente(retadorId))` → responde ephemeral "⚠️ Ya tenés un reto pendiente. Cancelalo o esperá a que se resuelva." y corta.
- Al construir los componentes del mensaje de desafío, se agrega un segundo botón "❌ Cancelar" (`ButtonStyle.Danger`) con `custom_id` `reto_cancelar_{retadorId}_{retadoId}_{apuesta}_{puntos}`, junto al "✅ Aceptar" ya existente.
- Después de `RespondAsync`, se obtiene el mensaje enviado (`await GetOriginalResponseAsync()`) y se llama a `_gestorPartidas.RegistrarReto(...)` con su `MensajeId`.
- Nuevo handler `[ComponentInteraction("reto_cancelar_*_*_*_*")] CancelarReto(ulong retadorId, ulong retadoId, int apuesta, int puntos)`:
  - Si `Context.User.Id != retadorId` → responde ephemeral "🚫 Solo quien retó puede cancelar el reto." y corta.
  - Llama a `_gestorPartidas.TryQuitarReto(retadorId, out _)`; si `false`, responde ephemeral "⚠️ Este reto ya no está disponible." Si `true`, edita el mensaje (mismo patrón `UpdateAsync` que usa `AceptarReto`) reemplazando el texto por "❌ *Reto cancelado por <@retadorId>.*" y vaciando los componentes.
- `AceptarReto`: antes de procesar la aceptación, llama a `_gestorPartidas.TryQuitarReto(retadorId, out _)`. Si devuelve `false` (expiró o fue cancelado por una carrera), responde ephemeral "⚠️ Este reto ya no está disponible." y corta, en vez de asumir que el botón visible implica que el reto sigue vigente.

### Mensaje final al expirar/cancelar

Se edita el mismo mensaje original del desafío (no se manda uno nuevo): se reemplaza el `Content` por el estado final y se vacían los `Components` (`new ComponentBuilder().Build()`), igual que ya hace `AceptarReto` al aceptar.

## Testing

Extender `tests/TrucoUruguayo.Bot.Tests/Servicios/GestorPartidasTests.cs` con casos para:
- `RegistrarReto` rechaza un segundo reto del mismo retador mientras el primero sigue pendiente.
- `TryQuitarReto` remueve el reto y permite que el mismo retador registre uno nuevo después.
- La expiración automática (con un `LimiteReto` reducido inyectable, o testeando el timer con control de tiempo) remueve el reto de `RetosPendientes`.

Los handlers de `TrucoModule` (interacciones de Discord) no tienen tests unitarios hoy — se mantiene esa convención; la lógica nueva de estado vive en `GestorPartidas`, que sí es testeable.
