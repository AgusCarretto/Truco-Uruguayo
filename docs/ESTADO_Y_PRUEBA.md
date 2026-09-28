# Estado del proyecto y guía de prueba — rama `claude-updates-deploy-listo`

## Segunda tanda — hardening para uso público real

Después de la primera tanda (deployment), se hizo una revisión pensando en "gente random
en internet usando esto, incluyendo trolls" y se encontraron 3 problemas reales más:

1. **Exploit: esquivar un Truco/Envido desfavorable con un botón viejo.** `IrseAlMazo` no
   chequeaba el estado de la ronda — un jugador con, por ejemplo, un Retruco pendiente de
   responder podía clickear un botón "Irse al Mazo" de un mensaje anterior (de antes de
   esa escalada) y rendirse al valor viejo y más bajo, en vez de tener que decidir
   Quiero/No Quiero al valor alto. Arreglado: ahora exige resolver el canto pendiente
   primero, igual que ya exigían las demás acciones.
2. **Condición de carrera entre clicks simultáneos.** `Ronda` no tiene ningún lock propio,
   y Discord.Net puede procesar dos interacciones del mismo canal en paralelo (doble click,
   reintento por lag de red). Un jugador impaciente clickeando dos veces rápido podía en
   teoría corromper el estado de la partida. Ahora hay un lock (semáforo) por canal que
   serializa todo lo que toca ese canal.
3. **Las monedas podían quedar negativas.** No había ningún piso — gastando en la tienda
   en el momento justo (mientras una apuesta está en juego) se podía terminar en negativo.
   Ahora está clampeado a 0 en la base.

También: mensaje de bienvenida cuando el bot se une a un servidor nuevo (antes entraba en
silencio, sin ninguna pista de por dónde arrancar).

## Qué se hizo en la primera tanda (deployment)

El juego en sí (reglas de Truco/Envido/Flor) ya estaba completo y probado de sesiones
anteriores. Lo que faltaba para poder **distribuirlo a otros servidores** era todo lo de
alrededor. Esto es lo que se arregló, en orden de importancia:

1. **Bloqueador real: los comandos solo funcionaban en un servidor.** `Program.cs` tenía
   `RegisterCommandsToGuildAsync(guildId)` con un `DISCORD_GUILD_ID` fijo — en cualquier
   otro servidor donde invitaras el bot, aparecía en la lista de miembros pero sin ningún
   comando `/`. Ahora `DISCORD_GUILD_ID` es **opcional**: si lo dejás vacío, los comandos
   se registran globalmente (tardan hasta 1h en aparecer la primera vez, pero funcionan en
   cualquier servidor). Si lo completás, sigue registrando solo ahí al instante — útil
   mientras estás vos probando.

2. **Bug de plata real: se perdía la apuesta si el bot se reiniciaba a mitad de partida.**
   Antes se descontaban las monedas de la apuesta a los dos jugadores apenas se aceptaba
   el reto. Como el estado de las partidas es en memoria (no hay persistencia), si el
   proceso se caía o reiniciabas el bot a mitad de una mano, esa plata desaparecía sin
   ningún reembolso. Ahora no se mueve nada hasta que la partida termina de verdad (fin de
   ronda normal o abandono por AFK) — recién ahí se le suma al ganador y se le resta al
   perdedor.

3. **README, LICENSE, Dockerfile/docker-compose.** No existía ninguno de los tres. Ahora:
   - `README.md`: cómo levantarlo (con Docker o a mano), cómo invitar el bot a un
     servidor (scopes/permisos exactos), lista de comandos, arquitectura.
   - `LICENSE`: MIT para el código. Las cartas del mazo básico son CC BY-SA 3.0 (ya estaba
     documentado en `assets/cartas/mazo_basico/ATRIBUCION.md`) — el LICENSE lo aclara.
   - `docker-compose.yml` + `Dockerfile`: `docker compose up -d --build` levanta Postgres
     (aplicando `schema.sql` solo, sin correrlo a mano) y el bot juntos.

4. **`TrucoUruguayo.slnx` no incluía el proyecto del Bot ni sus tests** — `dotnet build`/
   `dotnet test` a nivel solución se quedaban cortos sin avisar. Ya está arreglado, los 4
   proyectos están en la solución.

5. **Pulido chico:** `/ayuda` ahora también linkea "¿Cómo se juega?" (antes solo se veía
   una vez, en el mensaje de bienvenida a usuarios nuevos), las reglas básicas mencionan
   Flor (no estaba), la lista de comandos incluye `/titulos`/`/fondo_equipar`. El bot
   ahora muestra un status ("Jugando /truco para jugar | /ayuda") en la lista de
   miembros. Se agregó un chequeo de límites al jugar una carta que antes podía tirar un
   error feo si alguien clickeaba un botón de una vista vieja de su mano.

**No toqué las reglas del juego en sí** (Envido/Flor/Truco) — ya estaban bien y las
verificamos juntos con el harness de reglas en la sesión anterior.

## Cómo probarlo

### 1. Preparar Postgres y el bot

La forma más rápida es con Docker:

```bash
git checkout claude-updates-deploy-listo
cp .env.example .env
# completá DISCORD_TOKEN en ese .env (el de tu bot de prueba en el Developer Portal)
# completá tambien DISCORD_GUILD_ID con el ID de tu servidor de pruebas para que
# los comandos aparezcan al instante en vez de esperar hasta 1 hora
docker compose up -d --build
```

Si preferís correrlo directo con `dotnet run` (sin Docker), seguí la sección "Puesta en
marcha manual" del `README.md` — la diferencia es que ahí tenés que correr `schema.sql` a
mano contra tu Postgres.

### 2. Invitar el bot a tu servidor de pruebas

Developer Portal > tu app > OAuth2 > URL Generator: scopes `bot` + `applications.commands`,
permisos `Send Messages`, `Embed Links`, `Attach Files`, `Use Slash Commands`. Entrá a esa
URL y elegí tu servidor de pruebas.

### 3. Qué probar

**Lo nuevo de esta rama específicamente:**
- Reiniciá el bot (`docker compose restart bot` o Ctrl+C + `dotnet run` de nuevo) a mitad
  de una partida con apuesta y confirmá que a nadie se le movió la plata (antes del fix,
  la apuesta ya se había descontado al aceptar el reto).
- Con `DISCORD_GUILD_ID` vacío en el `.env`, confirmá que los comandos igual aparecen
  (puede tardar bastante la primera vez — para probar esto rápido es mejor dejar
  `DISCORD_GUILD_ID` puesto durante el desarrollo y solo sacarlo cuando ya estés conforme
  y quieras publicarlo).
- `/ayuda` → botón "¿Cómo se juega?" tiene que aparecer ahí ahora, no solo en la
  bienvenida.

**Flujo completo de una partida** (esto ya estaba probado por tests automáticos, pero
nunca de punta a punta jugando de verdad en Discord con dos cuentas):
- `/truco @otro_usuario apuesta:100 puntos:15`, aceptar el reto, jugar una mano completa
  cantando Envido y Flor si tocan, Truco/Retruco, terminar la ronda y confirmar que las
  monedas/XP/nivel se actualizan bien en `/perfil`.
- `/tienda`, comprar un fondo, `/fondo_equipar`, confirmar que la mesa se ve con ese fondo.
- Ganar una partida y confirmar que si corresponde aparece el mensaje de logro
  desbloqueado.

## Lo que dejé sin tocar (a propósito, o porque necesita algo de tu lado)

- **Bot solo soporta 1 vs 1.** No hay truco de a 4/6 ni equipos — no lo armé porque nadie
  lo pidió, sería una feature nueva grande, no algo que faltaba "terminar".
- **El estado de las partidas activas sigue siendo en memoria** — si el bot se reinicia a
  mitad de una partida, esa partida se pierde (hay que volver a jugar desde cero). Vos sos
  el host y reiniciar no es el modo normal de operar, así que esto no es un problema del
  día a día. **Mejora pendiente decidida (no implementada todavía):** en vez de persistir
  el estado completo de la `Ronda` en la base (cambio grande, complica el schema y la
  lógica para un caso raro), al arrancar el bot detectar que había partidas activas que se
  perdieron y devolverle a cada jugador su apuesta **más un extra** de compensación por el
  corte de conexión. Mucho más simple, sin tocar el modelo de partidas ni agregar tablas
  nuevas.
- **Avatar/ícono del bot**: eso se sube a mano en el Developer Portal (Bot > ícono), no es
  algo que se pueda hacer desde código.
- **Decisión tuya:** cuándo sacar `DISCORD_GUILD_ID` del `.env` para pasar a registro
  global (o sea, cuándo "publicarlo" de verdad para que cualquiera lo invite).

## Tests

`dotnet test TrucoUruguayo.slnx` corre los 195 tests (140 de Core, sin DB; 55 de Bot,
necesitan la Postgres con `schema.sql` aplicado). Todos verdes en esta rama.
