# Títulos/Sobrenombres desbloqueables por nivel

## Contexto

Con el sistema de niveles ya en pie (`Usuario.Nivel`, `NivelCalculadora`), se agrega un catálogo fijo de 9 títulos, cada uno desbloqueado a partir de cierto nivel. El jugador puede equipar uno (si tiene el nivel) y se muestra en `/perfil` y en el anuncio de `/truco`.

## Correcciones sobre el pedido original

- El texto épico dado (`usuario.TituloEquipado`) usa `usuario`, que en `TrucoModule.TrucoAsync` es el parámetro `IUser` del **retado** (`[Summary("usuario", "A quien desafias")] IUser usuario`) — no tiene `TituloEquipado` (es un `IUser` de Discord, no el modelo `Usuario`). El título que hay que mostrar es el del **retador** (`Context.User.Id`, quien aparece mencionado en el texto), que ya se fetchea en `TrucoAsync` como `usuarioRetador` (línea 70 actual). Se usa `usuarioRetador.TituloEquipado`, no se agrega ningún fetch nuevo.
- El texto épico se **fusiona** con el mensaje de desafío actual en vez de reemplazarlo — así no se pierde la apuesta ni el aviso de expiración en 30s que ya tiene ese mensaje (agregado en la feature de retos pendientes).

## Schema y modelo

`src/TrucoUruguayo.Bot/Datos/schema.sql`: agregar `titulo_equipado VARCHAR(100) DEFAULT NULL` al `CREATE TABLE usuarios`, más `ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS titulo_equipado VARCHAR(100) DEFAULT NULL;` debajo (mismo patrón idempotente que se usó para `nivel`, ya que no hay runner de migraciones). Hay que aplicar ese `ALTER` contra la Postgres local de desarrollo para que los tests de integración corran.

`src/TrucoUruguayo.Bot/Modelo/Usuario.cs`: agregar `public string? TituloEquipado { get; set; }`.

## `ConstantesTitulos.cs` (nuevo)

`src/TrucoUruguayo.Bot/Modelo/ConstantesTitulos.cs`:

```csharp
namespace TrucoUruguayo.Bot.Modelo;

public static class ConstantesTitulos
{
    public static readonly Dictionary<int, string> TitulosPorNivel = new()
    {
        { 1, "Pichón" },
        { 5, "Orejeador" },
        { 10, "Bocón" },
        { 15, "Rey del Envido" },
        { 20, "Cebador de Canarias Suave" },
        { 25, "Asador Oficial" },
        { 30, "Maestro del Retruco" },
        { 40, "Dueño de la Muestra" },
        { 50, "Leyenda del Truco" },
    };
}
```

## `UsuarioRepository.cs`

- `ObtenerUsuarioAsync`, `RegistrarUsuarioAsync` (RETURNING) y `ObtenerTopUsuariosAsync`: agregan `titulo_equipado AS TituloEquipado` a sus `SELECT`/`RETURNING`. El `INSERT` de `RegistrarUsuarioAsync` no se toca (columna nullable con `DEFAULT NULL`, Postgres la deja en `NULL` si no se lista).
- Nuevo método:

```csharp
public async Task<bool> EquiparTituloAsync(ulong discordId, string titulo)
```

Fetchea el usuario con `ObtenerUsuarioAsync`. Si no existe, o el título no está en `ConstantesTitulos.TitulosPorNivel.Values`, o el nivel del usuario es menor al `Key` cuyo `Value` es ese título (`TitulosPorNivel.First(kv => kv.Value == titulo).Key`), devuelve `false` sin tocar la base. Si cumple, hace `UPDATE usuarios SET titulo_equipado = @Titulo WHERE id = @Id` y devuelve `true`.

## `PerfilModule.cs`

**`/titulos`** (nuevo slash command): fetchea el usuario actual (mismo mensaje de "todavía no jugaste" que ya usa `/perfil` si no existe). Arma un embed listando `ConstantesTitulos.TitulosPorNivel` ordenado por nivel ascendente (`OrderBy(kv => kv.Key)`, no confiar en el orden de iteración del diccionario), un renglón por título:

- `✅ **{titulo}** (Nivel {nivel})` si `usuario.Nivel >= nivel`.
- `🔒 {titulo} — se desbloquea en Nivel {nivel}` si no.

**`/titulo_equipar [titulo]`** (nuevo slash command): parámetro `string titulo` con 9 `[Choice(...)]` estáticos (uno por título, valor = nombre = el mismo string del diccionario). Llama a `EquiparTituloAsync(Context.User.Id, titulo)`; si devuelve `true`, responde con éxito (`✅ Ahora tenés equipado: **{titulo}**`); si devuelve `false`, responde ephemeral indicando que le falta nivel (`🔒 Todavía no tenés el nivel para ese título.`).

**`/perfil`**: si `usuarioDb.TituloEquipado` no es `null`, se agrega al título del embed: `$"👤 {usuarioDb.Nombre} | 🏆 {usuarioDb.TituloEquipado}"`; si es `null`, el título del embed queda como está hoy (`$"👤 {usuarioDb.Nombre}"`).

## `TrucoModule.cs`

En `TrucoAsync`, el mensaje de desafío actual:

```csharp
await RespondAsync(
    $"⚔️ {usuario.Mention}, {Context.User.Mention} te desafía a una partida de Truco a {puntos} puntos por 🪙 {apuesta} monedas! (expira en 30s)",
    components: componentes);
```

pasa a incorporar el título del retador (usando `usuarioRetador.TituloEquipado`, ya fetcheado, con `"Jugador"` como default si no tiene ninguno equipado):

```csharp
await RespondAsync(
    $"⚔️ ¡El **{usuarioRetador.TituloEquipado ?? "Jugador"}** {Context.User.Mention} desafía a {usuario.Mention} a una partida de Truco a {puntos} puntos por 🪙 {apuesta} monedas! (expira en 30s)",
    components: componentes);
```

## Testing

`tests/TrucoUruguayo.Bot.Tests/Datos/UsuarioRepositoryTests.cs` (integración contra Postgres real, mismo patrón que el resto del archivo):

- `EquiparTituloAsync` con nivel suficiente → devuelve `true`, `ObtenerUsuarioAsync` refleja el nuevo `TituloEquipado`.
- `EquiparTituloAsync` con nivel insuficiente → devuelve `false`, `TituloEquipado` sigue en `null`.
- `EquiparTituloAsync` con un título que no existe en el diccionario → devuelve `false`, no rompe.

No se agregan tests automatizados para los comandos de Discord (`/titulos`, `/titulo_equipar`, `/perfil`, el anuncio de `/truco`) — mismo criterio que el resto de los módulos de interacción en este repo, sin cobertura unitaria hoy.
