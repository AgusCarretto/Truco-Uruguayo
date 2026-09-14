# Bienvenida a usuarios nuevos + boton "Como se juega"

**Fecha:** 2026-09-14
**Estado:** Aprobado

## Contexto

El bot de Discord (`TrucoUruguayo.Bot`) ya tiene un comando `/perfil` que
registra al usuario en la tabla `usuarios` (con 1000 monedas iniciales) la
primera vez que lo corre, si todavia no existia. Se pide generalizar ese
"primer contacto" a CUALQUIER comando, no solo `/perfil`, y agregar una
bienvenida con un boton que explique como se juega.

## Alcance de esta tanda

- Bienvenida + registro global de usuarios nuevos.
- Boton "Como se juega" con la explicacion.
- El texto de "como se juega" menciona `/truco @persona` como forma de
  desafiar a alguien, pero ese comando **no se construye en esta tanda** -
  se aclara en el propio texto que todavia se esta armando.
- No se agregan tests automatizados (el proyecto Bot no tiene tests hoy;
  es capa de presentacion de Discord.Net dificil de testear sin mockear la
  API completa). Se verifica corriendo el bot a mano.

## Flujo

1. Llega cualquier interaccion de slash command a
   `DiscordSocketClient.InteractionCreated`.
2. Si es un `SocketSlashCommand` (no un boton/componente), se consulta
   `UsuarioRepository.ObtenerUsuarioAsync(id)` ANTES de despachar el
   comando a `InteractionService.ExecuteCommandAsync`.
3. Si el usuario no existe:
   - Se registra con `RegistrarUsuarioAsync` (1000 monedas, como hoy).
   - Se responde a la interaccion (ephemeral) con el embed de bienvenida +
     boton "Como se juega".
   - **No se ejecuta el comando original en este golpe** - la persona lo
     vuelve a tirar despues de leer la bienvenida.
4. Si el usuario ya existe, se sigue el camino normal de hoy
   (`ExecuteCommandAsync`).
5. Los clicks de boton (`ComponentInteraction`) no pasan por el chequeo del
   paso 2 - van directo a `ExecuteCommandAsync`, porque para poder clickear
   el boton la persona ya tuvo que pasar por el paso 3 y estar registrada.

Consecuencia: `PerfilModule.PerfilAsync` ya no necesita su propio fallback
de auto-registro (`?? await RegistrarUsuarioAsync(...)`) - para cuando ese
codigo corre, el interceptor global ya garantizo que el usuario existe. Se
simplifica para asumir que `ObtenerUsuarioAsync` siempre devuelve un
usuario en ese punto.

## Componentes

- **`Program.cs`** (modificado): en el handler de `InteractionCreated`, se
  agrega el chequeo de usuario nuevo descripto arriba, solo para
  interacciones de tipo `SocketSlashCommand`. Una funcion local
  `EnviarBienvenidaAsync` arma y envia el embed + boton.
- **`Modulos/AyudaModule.cs`** (nuevo): modulo con
  `[ComponentInteraction("como_jugar")]` que responde (ephemeral) con el
  embed de "como se juega".
- **`Modulos/PerfilModule.cs`** (modificado): se saca el fallback de
  auto-registro, ya cubierto por el interceptor global.

## Contenido de los embeds

**Bienvenida** (al registrar por primera vez, cualquier comando):

> 🎉 **¡Bienvenido a Truco Uruguayo!**
> Te registramos y te regalamos 1000 monedas para arrancar. Tirá tu
> comando de nuevo cuando quieras.
>
> `[ ¿Cómo se juega? ]` (boton, custom id `como_jugar`)

**Como se juega** (al clickear el boton):

> **¿Cómo se juega?**
> Podés jugar de dos formas:
> • Con comandos como `/perfil` para ver tus monedas, victorias y
>   derrotas.
> • Desafiando directo a alguien con `/truco @persona` para arrancar una
>   partida 1 contra 1.
>
> *(`/truco` todavía lo estamos armando — por ahora `/perfil` ya anda)*

Ambos mensajes se responden con `ephemeral: true` (visibles solo para
quien los dispara), para no ensuciar canales compartidos.

## Fuera de alcance

- Comando `/truco @persona` en si (queda para otra tanda).
- Cualquier otro comando de juego.
- Tests automatizados de esta capa.
