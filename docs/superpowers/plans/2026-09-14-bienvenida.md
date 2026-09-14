# Bienvenida a Usuarios Nuevos + Boton Como Jugar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** La primera vez que un usuario tira CUALQUIER slash command, el bot lo registra, le responde solo con un embed de bienvenida + boton "Como se juega", y no ejecuta el comando original ese golpe.

**Architecture:** Un chequeo global en el handler `DiscordSocketClient.InteractionCreated` de `Program.cs` intercepta las interacciones de tipo `SocketSlashCommand` antes de despacharlas a `InteractionService.ExecuteCommandAsync`. Si el usuario no existe en la tabla `usuarios`, se registra y se responde con la bienvenida; si ya existe, sigue el camino normal de hoy. El boton de la bienvenida dispara un `ComponentInteraction` manejado por un modulo nuevo (`AyudaModule`), que no pasa por el chequeo (solo aplica a `SocketSlashCommand`).

**Tech Stack:** Discord.Net.WebSocket, Discord.Net.Interactions, Dapper/Npgsql (via `UsuarioRepository` ya existente). Sin frameworks de test nuevos.

## Global Constraints

- Ambos embeds (bienvenida y "como se juega") se responden con `ephemeral: true`.
- El custom id del boton es exactamente `como_jugar` — debe coincidir literal entre `Program.cs` (donde se crea el boton) y `AyudaModule.cs` (donde se maneja el click).
- El chequeo de "usuario nuevo" solo aplica a interacciones `SocketSlashCommand`. Los `ComponentInteraction` (clicks de boton) van directo a `ExecuteCommandAsync` sin pasar por el chequeo.
- Cuando se detecta un usuario nuevo, el comando original NO se ejecuta ese golpe — se corta despues de responder la bienvenida.
- El proyecto `TrucoUruguayo.Bot` no tiene tests automatizados hoy y esta tanda no le agrega ninguno (documentado en el spec). La verificacion de cada task es `dotnet build` (0 errores); la verificacion end-to-end final es manual, corriendo el bot.
- Todos los textos van en español, tal como estan escritos en este plan (no parafrasear).

---

### Task 1: Modulo de ayuda con el boton "Como se juega"

**Files:**
- Create: `src/TrucoUruguayo.Bot/Modulos/AyudaModule.cs`

**Interfaces:**
- Consumes: nada (modulo standalone, sin dependencias inyectadas).
- Produces: un handler de `ComponentInteraction` con custom id `"como_jugar"` que Task 2 va a referenciar (como string literal) al crear el boton en `Program.cs`.

- [ ] **Step 1: Crear el modulo**

Crear `src/TrucoUruguayo.Bot/Modulos/AyudaModule.cs` con este contenido exacto:

```csharp
using Discord;
using Discord.Interactions;

namespace TrucoUruguayo.Bot.Modulos;

public class AyudaModule : InteractionModuleBase<SocketInteractionContext>
{
    [ComponentInteraction("como_jugar")]
    public async Task ComoJugarAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("¿Cómo se juega?")
            .WithDescription(
                "Podés jugar de dos formas:\n\n" +
                "• Con comandos como `/perfil` para ver tus monedas, victorias y derrotas.\n" +
                "• Desafiando directo a alguien con `/truco @persona` para arrancar una partida 1 contra 1.\n\n" +
                "*(`/truco` todavía lo estamos armando — por ahora `/perfil` ya anda)*")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}
```

- [ ] **Step 2: Compilar**

Run (desde `src/TrucoUruguayo.Bot`): `dotnet build`
Expected: `Compilación correcta. 0 Advertencia(s). 0 Errores.`

- [ ] **Step 3: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/AyudaModule.cs
git commit -m "Agrega AyudaModule con el boton Como se juega"
```

---

### Task 2: Interceptor de bienvenida en Program.cs

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Program.cs`

**Interfaces:**
- Consumes: `UsuarioRepository.ObtenerUsuarioAsync(ulong): Task<Usuario?>` y `UsuarioRepository.RegistrarUsuarioAsync(ulong, string): Task<Usuario>` (ya existen en `Datos/UsuarioRepository.cs`). Custom id `"como_jugar"` de Task 1 (usado como string literal, sin referencia de codigo directa).
- Produces: comportamiento del pipeline de interacciones — a partir de este task, `PerfilModule` (Task 3) puede asumir que el usuario siempre existe en DB para cuando su handler corre.

- [ ] **Step 1: Leer el archivo actual**

Leer `src/TrucoUruguayo.Bot/Program.cs` para confirmar el estado exacto antes de editar (debe tener el `client.Log`, `interactions.Log`, `client.InteractionCreated`, `client.Ready` y el chequeo de conexion a DB ya agregados en la tanda anterior).

- [ ] **Step 2: Agregar la resolucion de `UsuarioRepository`**

Despues de la linea `var interactions = services.GetRequiredService<InteractionService>();`, agregar:

```csharp
var usuarioRepository = services.GetRequiredService<UsuarioRepository>();
```

- [ ] **Step 3: Reemplazar el handler de `InteractionCreated`**

Reemplazar:

```csharp
client.InteractionCreated += async interaction =>
{
    var context = new SocketInteractionContext(client, interaction);
    await interactions.ExecuteCommandAsync(context, services);
};
```

por:

```csharp
client.InteractionCreated += async interaction =>
{
    if (interaction is SocketSlashCommand
        && await usuarioRepository.ObtenerUsuarioAsync(interaction.User.Id) is null)
    {
        await usuarioRepository.RegistrarUsuarioAsync(interaction.User.Id, interaction.User.Username);
        await EnviarBienvenidaAsync(interaction);
        return;
    }

    var context = new SocketInteractionContext(client, interaction);
    await interactions.ExecuteCommandAsync(context, services);
};
```

- [ ] **Step 4: Agregar la funcion local `EnviarBienvenidaAsync`**

Agregar esta funcion local en cualquier punto del archivo despues de las declaraciones de `using` (por ejemplo, justo antes de la linea `await client.LoginAsync(TokenType.Bot, token);`):

```csharp
async Task EnviarBienvenidaAsync(SocketInteraction interaction)
{
    var embed = new EmbedBuilder()
        .WithTitle("🎉 ¡Bienvenido a Truco Uruguayo!")
        .WithDescription("Te registramos y te regalamos 1000 monedas para arrancar. Tirá tu comando de nuevo cuando quieras.")
        .WithColor(Color.Gold)
        .Build();

    var componentes = new ComponentBuilder()
        .WithButton("¿Cómo se juega?", "como_jugar", ButtonStyle.Primary)
        .Build();

    await interaction.RespondAsync(embed: embed, components: componentes, ephemeral: true);
}
```

- [ ] **Step 5: Compilar**

Run (desde `src/TrucoUruguayo.Bot`): `dotnet build`
Expected: `Compilación correcta. 0 Advertencia(s). 0 Errores.`

- [ ] **Step 6: Commit**

```bash
git add src/TrucoUruguayo.Bot/Program.cs
git commit -m "Intercepta el primer comando de un usuario nuevo con la bienvenida"
```

---

### Task 3: Simplificar PerfilModule

**Files:**
- Modify: `src/TrucoUruguayo.Bot/Modulos/PerfilModule.cs`

**Interfaces:**
- Consumes: garantia de Task 2 — para cuando `PerfilAsync` corre, el usuario ya existe en la tabla `usuarios` (el interceptor global lo registro si hacia falta).
- Produces: nada nuevo, solo simplifica codigo existente.

- [ ] **Step 1: Leer el archivo actual**

Leer `src/TrucoUruguayo.Bot/Modulos/PerfilModule.cs` para confirmar el contenido exacto antes de editar.

- [ ] **Step 2: Sacar el fallback de auto-registro**

Reemplazar:

```csharp
        var usuario = await _usuarioRepository.ObtenerUsuarioAsync(Context.User.Id)
            ?? await _usuarioRepository.RegistrarUsuarioAsync(Context.User.Id, Context.User.Username);
```

por:

```csharp
        var usuario = (await _usuarioRepository.ObtenerUsuarioAsync(Context.User.Id))!;
```

- [ ] **Step 3: Compilar**

Run (desde `src/TrucoUruguayo.Bot`): `dotnet build`
Expected: `Compilación correcta. 0 Advertencia(s). 0 Errores.` (el `!` suprime el warning de nullable; si aparece algun warning de nullable, revisar que el `!` quedo bien puesto sobre el resultado del `await`, no sobre `Context.User.Id`).

- [ ] **Step 4: Commit**

```bash
git add src/TrucoUruguayo.Bot/Modulos/PerfilModule.cs
git commit -m "Simplifica PerfilModule ahora que el registro es global"
```

---

### Task 4: Verificacion manual end-to-end

**Files:** ninguno (solo verificacion, sin cambios de codigo).

- [ ] **Step 1: Confirmar que no hay un usuario de prueba ya registrado**

El registro se identifica por `id` (el Discord user id del que va a probar). Si ese id ya esta en la tabla `usuarios` de una prueba anterior, hay que borrarlo para simular "usuario nuevo": `DELETE FROM usuarios WHERE id = <tu_discord_id>;`

- [ ] **Step 2: Correr el bot**

Run (desde `src/TrucoUruguayo.Bot`): `dotnet run`
Expected en consola: `Conexion a la base de datos OK.` y despues los logs de `Gateway Connected` / `Ready`.

- [ ] **Step 3: Tirar `/perfil` desde Discord con el usuario de prueba**

Expected: responde SOLO el embed "🎉 ¡Bienvenido a Truco Uruguayo!" con el boton "¿Cómo se juega?", visible solo para quien lo tiro (ephemeral). El comando `/perfil` NO muestra la ficha de stats en este golpe.

- [ ] **Step 4: Clickear el boton "¿Cómo se juega?"**

Expected: responde (ephemeral) el embed "¿Cómo se juega?" con la explicacion de `/perfil` y `/truco @persona`.

- [ ] **Step 5: Tirar `/perfil` de nuevo**

Expected: esta vez responde con la ficha de stats normal (Nombre, Monedas: 1000, Victorias: 0, Derrotas: 0) — el usuario ya estaba registrado, no vuelve a pasar por la bienvenida.

- [ ] **Step 6: Cortar el bot**

`Ctrl+C` en la terminal donde corre `dotnet run`.
