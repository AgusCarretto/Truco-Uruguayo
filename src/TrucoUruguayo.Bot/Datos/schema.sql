CREATE TABLE IF NOT EXISTS usuarios (
    id BIGINT PRIMARY KEY,
    nombre TEXT NOT NULL,
    monedas INTEGER NOT NULL DEFAULT 0,
    victorias INTEGER NOT NULL DEFAULT 0,
    derrotas INTEGER NOT NULL DEFAULT 0,
    xp INTEGER NOT NULL DEFAULT 0,
    nivel INTEGER NOT NULL DEFAULT 1,
    titulo_equipado VARCHAR(100) DEFAULT NULL,
    fondo_equipado VARCHAR(30) NOT NULL DEFAULT 'fondo_madera'
);

-- No hay runner de migraciones: estos ALTER/DROP idempotentes hacen que volver a correr
-- schema.sql contra una base ya existente agregue o saque columnas sin romper nada.
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS nivel INTEGER NOT NULL DEFAULT 1;
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS titulo_equipado VARCHAR(100) DEFAULT NULL;
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS fondo_equipado VARCHAR(30) NOT NULL DEFAULT 'fondo_madera';

-- El mazo clasico se dio de baja (se borraron sus assets): saca la columna de quien lo
-- tuviera equipado, para no dejar una referencia a un mazo que ya no existe en disco.
ALTER TABLE usuarios DROP COLUMN IF EXISTS mazo_equipado;

CREATE TABLE IF NOT EXISTS recompensas_diarias (
    usuario_id BIGINT PRIMARY KEY,
    ultimo_reclamo TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS partidas_historico (
    id SERIAL PRIMARY KEY,
    ganador_id BIGINT NOT NULL,
    perdedor_id BIGINT NOT NULL,
    apuesta INTEGER NOT NULL,
    fecha TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS tienda_items (
    id SERIAL PRIMARY KEY,
    nombre TEXT NOT NULL,
    descripcion TEXT NOT NULL,
    precio INTEGER NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    emoji VARCHAR(10) NOT NULL DEFAULT '🛍️'
);

ALTER TABLE tienda_items ADD COLUMN IF NOT EXISTS emoji VARCHAR(10) NOT NULL DEFAULT '🛍️';

CREATE TABLE IF NOT EXISTS inventario_usuarios (
    usuario_id BIGINT NOT NULL,
    item_id INTEGER NOT NULL,
    fecha_compra TIMESTAMPTZ NOT NULL,
    equipado BOOLEAN DEFAULT FALSE,
    PRIMARY KEY (usuario_id, item_id)
);

-- El mazo clasico se dio de baja: si quedo como item de tienda de una corrida anterior de
-- este schema, se lo saca (y el inventario/equipamiento que lo referenciaba) para que no
-- se pueda seguir comprando ni aparezca en /tienda.
DELETE FROM inventario_usuarios
WHERE item_id IN (SELECT id FROM tienda_items WHERE nombre = 'Mazo Clásico');
DELETE FROM tienda_items WHERE nombre = 'Mazo Clásico';

-- "Mazo Dorado", "Título: Campeón" y "Emoji Personalizado" son placeholders de una etapa
-- muy temprana del proyecto (de antes de que existiera este schema.sql): ninguno de los
-- tres esta conectado a ningun efecto real (a diferencia de los fondos, que si cambian la
-- mesa via fondo_equipado) -- comprarlos y equiparlos no hace nada. Se sacan de la tienda
-- junto con cualquier compra/equipamiento que los referenciara.
DELETE FROM inventario_usuarios
WHERE item_id IN (SELECT id FROM tienda_items WHERE nombre IN ('Mazo Dorado', 'Título: Campeón', 'Emoji Personalizado'));
DELETE FROM tienda_items WHERE nombre IN ('Mazo Dorado', 'Título: Campeón', 'Emoji Personalizado');

-- Fondos de mesa comprables (ver GeneradorImagenes / UsuarioRepository.EquiparFondoAsync).
-- El fondo de madera original es gratis (default de todos), estos son los nuevos. No hay
-- UNIQUE en nombre, asi que se usa WHERE NOT EXISTS para que insertarlos sea idempotente
-- igual que los ALTER de arriba.
INSERT INTO tienda_items (nombre, descripcion, precio)
SELECT 'Fondo Verde Liso', 'Cambia el fondo de la mesa a un pano verde liso.', 4000
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Fondo Verde Liso');

INSERT INTO tienda_items (nombre, descripcion, precio)
SELECT 'Fondo Póker', 'Cambia el fondo de la mesa a un tapete de póker.', 8000
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Fondo Póker');

INSERT INTO tienda_items (nombre, descripcion, precio)
SELECT 'Fondo Madera Oscura', 'Cambia el fondo de la mesa a una madera oscura.', 15000
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Fondo Madera Oscura');

-- assets/fondos/fondo_maderaOscura.jpg en realidad es un AVIF con la extension cambiada
-- (SixLabors.ImageSharp no lo puede decodificar, revienta con UnknownImageFormatException
-- al intentar usarlo). Desactivado hasta que se reemplace por un JPEG/PNG de verdad -- ver
-- docs/ESTADO_Y_PRUEBA.md. Sacar este UPDATE (o poner activo = TRUE a mano) cuando este
-- arreglado.
UPDATE tienda_items SET activo = FALSE WHERE nombre = 'Fondo Madera Oscura';

-- Emojis mas propios que el generico 🛍️/🎁 de antes, uno por item segun lo que representa.
UPDATE tienda_items SET emoji = '🟩' WHERE nombre = 'Fondo Verde Liso';
UPDATE tienda_items SET emoji = '♠️' WHERE nombre = 'Fondo Póker';
UPDATE tienda_items SET emoji = '🪵' WHERE nombre = 'Fondo Madera Oscura';

-- Titulos comprables (independientes de los titulos por nivel de ConstantesTitulos). Se
-- equipan con /titulo_equipar igual que los de nivel; UsuarioRepository.EquiparTituloAsync
-- valida la compra buscando el item 'Título: <nombre>' en el inventario. El nombre en la
-- tienda lleva el prefijo para que se lea claro en /tienda, pero lo que queda guardado en
-- usuarios.titulo_equipado es el nombre sin el prefijo (ej. "Millonario").
INSERT INTO tienda_items (nombre, descripcion, precio, emoji)
SELECT 'Título: Adinerado', 'Titulo comprable. Se muestra en tu perfil junto a tu nombre.', 6000, '💵'
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Título: Adinerado');

INSERT INTO tienda_items (nombre, descripcion, precio, emoji)
SELECT 'Título: Millonario', 'Titulo comprable. Se muestra en tu perfil junto a tu nombre.', 20000, '💰'
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Título: Millonario');

INSERT INTO tienda_items (nombre, descripcion, precio, emoji)
SELECT 'Título: Magnate', 'Titulo comprable. Se muestra en tu perfil junto a tu nombre.', 50000, '💎'
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Título: Magnate');

INSERT INTO tienda_items (nombre, descripcion, precio, emoji)
SELECT 'Título: Billonario', 'Titulo comprable. Se muestra en tu perfil junto a tu nombre.', 100000, '🏦'
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Título: Billonario');

-- Insignias: cosmeticos comprables que se equipan con el /equipar generico y aparecen en
-- /perfil bajo "Equipamiento Activo" con su propio emoji y nombre. A diferencia de los
-- fondos y titulos (uno solo a la vez, con su propio comando exclusivo), se pueden tener
-- varias insignias equipadas al mismo tiempo, ya que /equipar solo prende/apaga el flag de
-- cada item -- ver TiendaRepository.AlternarEquipamientoAsync.
INSERT INTO tienda_items (nombre, descripcion, precio, emoji)
SELECT 'Insignia: Billete Dorado', 'Insignia cosmetica que aparece junto a tu nombre en /perfil.', 3000, '💵'
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Insignia: Billete Dorado');

INSERT INTO tienda_items (nombre, descripcion, precio, emoji)
SELECT 'Insignia: Lluvia de Billetes', 'Insignia cosmetica que aparece junto a tu nombre en /perfil.', 12000, '🤑'
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Insignia: Lluvia de Billetes');

-- Antes de este fix, los fondos y titulos comprables tambien se podian "equipar" con el
-- /equipar generico (sin ningun efecto real en el juego, ya que lo unico que cambia la
-- mesa/el titulo mostrado es fondo_equipado/titulo_equipado en usuarios) -- eso dejaba
-- "Equipamiento Activo" en /perfil mostrando fondos viejos que no eran el que realmente se
-- usaba, causando confusion ("compre Poker pero no me cambia nada"). Se desequipan los que
-- hayan quedado asi marcados de antes de este fix; a partir de ahora TiendaRepository ya no
-- deja tocarlos con /equipar.
UPDATE inventario_usuarios
SET equipado = FALSE
WHERE item_id IN (SELECT id FROM tienda_items WHERE nombre LIKE 'Fondo %' OR nombre LIKE 'Título: %');

CREATE TABLE IF NOT EXISTS estadisticas_usuario (
    discord_id BIGINT PRIMARY KEY,
    partidas_ganadas INT NOT NULL DEFAULT 0,
    flores_cantadas INT NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS logros_catalogo (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    estadistica_clave VARCHAR(50) NOT NULL,
    meta INT NOT NULL,
    recompensa_monedas INT NOT NULL,
    emoji VARCHAR(10) NOT NULL
);

CREATE TABLE IF NOT EXISTS usuario_logros (
    discord_id BIGINT NOT NULL,
    logro_id INT NOT NULL,
    fecha TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (discord_id, logro_id)
);

INSERT INTO logros_catalogo (nombre, estadistica_clave, meta, recompensa_monedas, emoji)
SELECT 'Primer Triunfo', 'partidas_ganadas', 1, 500, '🏆'
WHERE NOT EXISTS (SELECT 1 FROM logros_catalogo WHERE nombre = 'Primer Triunfo');

INSERT INTO logros_catalogo (nombre, estadistica_clave, meta, recompensa_monedas, emoji)
SELECT 'Veterano', 'partidas_ganadas', 10, 2000, '👑'
WHERE NOT EXISTS (SELECT 1 FROM logros_catalogo WHERE nombre = 'Veterano');

INSERT INTO logros_catalogo (nombre, estadistica_clave, meta, recompensa_monedas, emoji)
SELECT 'Jardinero', 'flores_cantadas', 25, 1500, '🌸'
WHERE NOT EXISTS (SELECT 1 FROM logros_catalogo WHERE nombre = 'Jardinero');
