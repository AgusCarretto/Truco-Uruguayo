# Estado del proyecto y guía de prueba — rama `claude-updates-deploy-listo`

## Cuarta tanda — 2 bugs reportados jugando en vivo

1. **Fondos "equipados" de más, y el de Póker "no cambiaba nada".** El bug real no era el
   renderizado (Póker sí se estaba dibujando bien, con sus fichas) sino que `/equipar` (el
   comando genérico de inventario) dejaba marcar como "equipado" cualquier item, incluidos
   fondos y títulos comprados — sin que eso tuviera ningún efecto real, porque lo único que
   cambia la mesa es `usuarios.fondo_equipado` (que se setea con `/fondo_equipar`, no con
   `/equipar`). Resultado: "Equipamiento Activo" en `/perfil` mostraba fondos viejos que no
   eran el que en verdad se usaba en la partida, dando la sensación de que equipar Póker "no
   hacía nada". Arreglado: `/equipar` ahora rechaza fondos y títulos comprables con un
   mensaje que indica el comando correcto (`/fondo_equipar` / `/titulo_equipar`); quedan
   reservados para cosméticos sin exclusividad como las insignias. Un `UPDATE` en
   `schema.sql` desequipa los que hayan quedado mal marcados de antes de este fix.
2. **Un botón viejo de "Ver mis cartas" podía jugar una carta distinta a la del botón.**
   Los botones de jugar carta identificaban la carta por posición en la mano (`jugar_carta_0`,
   `jugar_carta_1`, ...). Al jugar una carta, la mano se acorta y los índices se corren — un
   click en un botón de una vista vieja (de antes de jugar otra carta) podía terminar jugando
   la carta que *ahora* está en esa posición, no la que decía el botón. Arreglado: el botón
   ahora identifica la carta por número+palo (`jugar_carta_7_Espada`), no por posición. Un
   botón de una carta ya jugada simplemente no matchea nada y tira un aviso claro en vez de
   jugar la carta equivocada.
   - Sobre "bloquear los botones viejos": no hace falta deshabilitarlos activamente — con la
     carta identificada por número+palo, un botón viejo de una carta que ya no está en la
     mano deja de tener efecto (da el aviso de "esa carta ya no está en tu mano"), y uno de
     una carta que sigue en la mano juega esa carta correctamente sin importar en qué
     posición haya quedado. Deshabilitar los mensajes viejos de verdad requeriría guardar y
     editar cada mensaje anterior — no se hizo porque el fix de arriba ya cubre el problema
     real (jugar la carta equivocada), y agregar eso sería más complejidad por poco más.

## Tercera tanda — iconos, títulos y insignias comprables

- **Cada item de `/tienda` tiene ahora su propio emoji** (columna `tienda_items.emoji`),
  en vez del genérico 🛍️/🎁 fijo que se usaba antes sin importar qué fuera el item. Se ve
  en `/tienda`, `/inventario` y en "Equipamiento Activo" de `/perfil`.
- **4 títulos comprables nuevos** además de los títulos por nivel: 💵 Adinerado (6.000),
  💰 Millonario (20.000), 💎 Magnate (50.000), 🏦 Billonario (100.000). Se compran con
  `/comprar` y se equipan con `/titulo_equipar` (mismo comando que los títulos por nivel,
  ahora con estas 4 opciones nuevas en el desplegable). `UsuarioRepository.EquiparTituloAsync`
  revisa primero si es un título por nivel (`ConstantesTitulos`) y si no, si el jugador
  compró el item `"Título: " + nombre` en la tienda.
- **2 insignias comprables** (cosmético, sin efecto en el juego): 💵 Insignia: Billete
  Dorado (3.000) y 🤑 Insignia: Lluvia de Billetes (12.000). Se compran con `/comprar` y se
  equipan con el `/equipar` genérico — al equiparlas aparecen en "Equipamiento Activo" en
  `/perfil`. A diferencia de los títulos (uno solo a la vez), se pueden tener varias
  insignias equipadas al mismo tiempo. (Nota: originalmente los fondos también pasaban por
  `/equipar` — eso causó el bug de la cuarta tanda, ver arriba; ahora `/equipar` es
  exclusivo de insignias.)
- Se sacaron 3 items placeholder de una etapa muy vieja del proyecto que estaban en la base
  de datos pero no conectados a ningún efecto real: "Mazo Dorado", "Título: Campeón" y
  "Emoji Personalizado". Si alguien ya los había comprado, no se les cobró nada especial
  por eso (avisar si hay que reponer monedas a alguien que los tuviera).
- El marcador de puntos ya no usa "palitos" (`|||`) al lado del número — con un par de
  manos esa cadena se hacía larga y tapaba el resto del mensaje. Ahora es solo el número.
- `/inventario` ahora muestra el `#id` de cada item (antes solo aparecía en `/tienda`,
  haciendo difícil saber qué ID pasarle a `/equipar`).

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
- **Cierre prolijo**: arrancá una partida con apuesta, y en vez de matar el proceso de
  golpe, hacé un cierre normal (Ctrl+C en `dotnet run`, o `docker compose stop bot`).
  Tiene que aparecer un mensaje en el canal de la partida avisando que se cortó y sumando
  el extra, y en `/perfil` de ambos jugadores el saldo tiene que reflejarlo. Con
  `docker compose kill bot` (eso sí mata de golpe, sin SIGTERM) no debería pasar nada de
  esto — es el caso esperado que queda sin cubrir.

**Flujo completo de una partida** (esto ya estaba probado por tests automáticos, pero
nunca de punta a punta jugando de verdad en Discord con dos cuentas):
- `/truco @otro_usuario apuesta:100 puntos:15`, aceptar el reto, jugar una mano completa
  cantando Envido y Flor si tocan, Truco/Retruco, terminar la ronda y confirmar que las
  monedas/XP/nivel se actualizan bien en `/perfil`.
- `/tienda`, comprar un fondo, `/fondo_equipar`, confirmar que la mesa se ve con ese fondo.
- Ganar una partida y confirmar que si corresponde aparece el mensaje de logro
  desbloqueado.

## Cierre prolijo del bot (implementado)

El estado de las partidas sigue siendo en memoria (no se persiste `Ronda` en la base — se
decidió que no vale la pena la complejidad para un caso raro). Pero ahora, en vez de que
un reinicio simplemente mate las partidas en curso sin avisar:

- `Program.cs` engancha **SIGTERM** (lo que manda `docker stop`/`docker compose down`,
  con ~10s de gracia antes de que Docker mate el proceso) y **SIGINT** (Ctrl+C en local).
  En vez de cortar de golpe, el bot frena ahí, liquida las partidas activas, y recién
  después se desconecta.
- `GestorPartidas.CompensarPartidasActivasPorCierreAsync()`: por cada partida activa, le
  suma a **cada uno** de los dos jugadores el valor de la apuesta como compensación (la
  apuesta en sí nunca se había descontado — ver el fix de la primera tanda — así que esto
  es puro extra, nadie queda mejor que otro) y manda un mensaje al canal explicando que la
  partida se cortó por un reinicio y por qué. `/perfil` va a reflejar el extra al toque.
- Si el bot se cae de golpe (crash, `kill -9`, corte de luz) en vez de cerrarse prolijo,
  esto no llega a correr — para eso no hay nada que hacer sin persistir partidas de verdad,
  y quedó fuera de alcance a propósito (ver más abajo).

## Lo que dejé sin tocar (a propósito, o porque necesita algo de tu lado)

- **Bot solo soporta 1 vs 1.** No hay truco de a 4/6 ni equipos — no lo armé porque nadie
  lo pidió, sería una feature nueva grande, no algo que faltaba "terminar".
- **Persistencia real de partidas en curso** (para sobrevivir un crash, no solo un cierre
  prolijo) seguiría siendo un cambio grande — nueva tabla, serializar/deserializar una
  `Ronda` a mitad de resolución de un Envido/Truco/Flor pendiente. Con el cierre prolijo ya
  cubierto, esto solo importaría para el caso de un crash de verdad, que es mucho menos
  frecuente — lo dejo anotado pero no lo armé.
- **Avatar/ícono del bot**: eso se sube a mano en el Developer Portal (Bot > ícono), no es
  algo que se pueda hacer desde código.
- **Decisión tuya:** cuándo sacar `DISCORD_GUILD_ID` del `.env` para pasar a registro
  global (o sea, cuándo "publicarlo" de verdad para que cualquiera lo invite).

## Tests

`dotnet test TrucoUruguayo.slnx` corre los 201 tests (140 de Core, sin DB; 61 de Bot,
necesitan la Postgres con `schema.sql` aplicado). Todos verdes en esta rama.
