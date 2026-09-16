CREATE TABLE IF NOT EXISTS usuarios (
    id BIGINT PRIMARY KEY,
    nombre TEXT NOT NULL,
    monedas INTEGER NOT NULL DEFAULT 0,
    victorias INTEGER NOT NULL DEFAULT 0,
    derrotas INTEGER NOT NULL DEFAULT 0,
    xp INTEGER NOT NULL DEFAULT 0,
    nivel INTEGER NOT NULL DEFAULT 1,
    titulo_equipado VARCHAR(100) DEFAULT NULL,
    mazo_equipado VARCHAR(20) NOT NULL DEFAULT 'mazo_basico'
);

-- No hay runner de migraciones: estos ALTER idempotentes hacen que volver a correr
-- schema.sql contra una base ya existente agregue las columnas sin romper nada.
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS nivel INTEGER NOT NULL DEFAULT 1;
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS titulo_equipado VARCHAR(100) DEFAULT NULL;
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS mazo_equipado VARCHAR(20) NOT NULL DEFAULT 'mazo_basico';

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
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS inventario_usuarios (
    usuario_id BIGINT NOT NULL,
    item_id INTEGER NOT NULL,
    fecha_compra TIMESTAMPTZ NOT NULL,
    equipado BOOLEAN DEFAULT FALSE,
    PRIMARY KEY (usuario_id, item_id)
);

-- Item comprable de la tienda para desbloquear el mazo clasico (ver GeneradorImagenes /
-- UsuarioRepository.EquiparMazoAsync). No hay UNIQUE en nombre, asi que se usa WHERE NOT
-- EXISTS para que insertarlo sea idempotente igual que los ALTER de arriba.
INSERT INTO tienda_items (nombre, descripcion, precio)
SELECT 'Mazo Clásico', 'Cambia el arte de tus cartas al mazo clasico espanol, con dorso propio.', 8000
WHERE NOT EXISTS (SELECT 1 FROM tienda_items WHERE nombre = 'Mazo Clásico');
