# Truco Uruguayo Bot

Bot de Discord para jugar al Truco Uruguayo (con Flor y sistema de Piezas) 1 contra 1,
con apuestas en moneda virtual, economía, tienda de cosméticos, niveles, títulos y logros.

Las manos y la mesa se juegan con imágenes generadas en vivo (cartas + fondo de mesa
comprable), no con texto.

## Funcionalidades

- **Truco completo**: Envido / Real Envido / Falta Envido, Flor / Con Flor Envido /
  Contra Flor al Resto (con la obligación real de cantar Flor antes que cualquier otra
  cosa), Truco / Retruco / Vale 4 con escalada "tipo tenis", Ir al Mazo. Sistema de
  Piezas según el palo de la muestra.
- **Apuestas en moneda virtual**: se desafía a otro jugador por una cantidad de monedas;
  gana quien gana la partida. El dinero recién se mueve al terminar la partida (si el bot
  se reinicia a mitad de una mano, nadie pierde monedas).
- **Economía**: `/diaria` (monedas gratis cada 24h), `/ranking`, `/historial`.
- **Tienda y cosméticos**: `/tienda`, `/comprar`, `/inventario`, `/equipar` — fondos de
  mesa comprables (`/fondo_equipar`).
- **Progresión**: XP y niveles por partida jugada, títulos desbloqueables por nivel
  (`/titulos`, `/titulo_equipar`), logros con insignias visibles en `/perfil`.
- **Ayuda in-app**: `/ayuda` con reglas, valor de las cartas y explicación de comandos.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (si vas a correrlo sin Docker)
- PostgreSQL (o Docker, que lo levanta por vos)
- Una aplicación de bot creada en el [Discord Developer Portal](https://discord.com/developers/applications)

## Puesta en marcha rápida (Docker)

1. Creá una aplicación de bot en el [Discord Developer Portal](https://discord.com/developers/applications),
   andá a **Bot** y copiá el token.
2. En la raíz del repo, copiá `.env.example` a `.env` y completá `DISCORD_TOKEN`.
3. `docker compose up -d --build`

Eso levanta Postgres (aplicando `schema.sql` automáticamente la primera vez) y el bot.
Los comandos se registran globalmente por default — tardan hasta 1 hora en aparecer la
primera vez en cada servidor. Si querés iterar rápido mientras desarrollás, completá
también `DISCORD_GUILD_ID` en el `.env` (ver el archivo de ejemplo para el detalle).

## Puesta en marcha manual (sin Docker)

1. Tené una instancia de PostgreSQL corriendo y creá una base de datos.
2. Aplicá el schema: `psql -U <usuario> -d <base> -f src/TrucoUruguayo.Bot/Datos/schema.sql`
   (es idempotente — correrlo de nuevo después de un `git pull` no rompe nada, así se
   agregan columnas/tablas nuevas).
3. Copiá `src/TrucoUruguayo.Bot/.env.example` a `src/TrucoUruguayo.Bot/.env` y completá
   `DISCORD_TOKEN` y `DB_CONNECTION_STRING`.
4. `dotnet run --project src/TrucoUruguayo.Bot`

## Invitar el bot a un servidor

En el Developer Portal, pestaña **OAuth2 > URL Generator**:

- **Scopes**: `bot` y `applications.commands`.
- **Bot Permissions**: `Send Messages`, `Embed Links`, `Attach Files` (para las imágenes
  de cartas y mesa), `Use Slash Commands`.

No hace falta activar ningún **Privileged Gateway Intent** (el bot solo usa el intent
`Guilds`, no lee contenido de mensajes).

Con el registro global de comandos (default, sin `DISCORD_GUILD_ID`), cualquiera que
invite el bot a su servidor con esa URL va a poder usarlo sin que vos toques nada más.

## Comandos

| Comando | Qué hace |
|---|---|
| `/truco @usuario apuesta puntos` | Desafía a otro jugador a una partida 1 contra 1 |
| `/perfil [usuario]` | Monedas, victorias/derrotas, nivel, insignias, título y fondo equipados |
| `/ayuda` | Reglas, valor de las cartas y explicación de comandos |
| `/diaria` | Reclama 500 monedas gratis cada 24 horas |
| `/tienda` | Lista los ítems comprables |
| `/comprar item_id` | Compra un ítem de la tienda |
| `/inventario` | Lista lo que compraste |
| `/equipar item_id` | Equipa/desequipa un ítem del inventario |
| `/fondo_equipar` | Elige qué fondo de mesa comprado usar |
| `/titulos` | Títulos desbloqueados/bloqueados según tu nivel |
| `/titulo_equipar` | Equipa un título desbloqueado |
| `/ranking categoria` | Top 10 global por monedas o XP |
| `/historial [usuario]` | Últimas 5 partidas jugadas |

El resto de la partida (ver mano, jugar carta, cantar Envido/Flor/Truco, responder) se
juega con los botones y menús que manda el bot en cada mensaje, no con comandos sueltos.

## Arquitectura

- `src/TrucoUruguayo.Core`: reglas del juego, sin ninguna dependencia de Discord ni de
  base de datos (`Ronda`, `GestorDeJerarquia`, `Mazo`). Es lo que se testea más a fondo.
- `src/TrucoUruguayo.Bot`: todo lo que sí depende de Discord.Net y Postgres — módulos de
  comandos, repositorios (Dapper), generación de imágenes (SixLabors.ImageSharp).
- `tests/`: xUnit. `TrucoUruguayo.Core.Tests` son puros (sin DB). `TrucoUruguayo.Bot.Tests`
  necesitan una Postgres local con el schema ya aplicado.

## Correr los tests

```bash
dotnet test TrucoUruguayo.slnx
```

(`TrucoUruguayo.Bot.Tests` necesita `DB_CONNECTION_STRING` en `src/TrucoUruguayo.Bot/.env`
apuntando a una Postgres con `schema.sql` ya aplicado.)

## Licencia

Código bajo MIT (ver `LICENSE`). Las imágenes de cartas en
`src/TrucoUruguayo.Bot/assets/cartas/mazo_basico/` son CC BY-SA 3.0 — ver el
`ATRIBUCION.md` de esa carpeta antes de redistribuirlas.
