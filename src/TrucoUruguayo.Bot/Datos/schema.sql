CREATE TABLE IF NOT EXISTS usuarios (
    id BIGINT PRIMARY KEY,
    nombre TEXT NOT NULL,
    monedas INTEGER NOT NULL DEFAULT 0,
    victorias INTEGER NOT NULL DEFAULT 0,
    derrotas INTEGER NOT NULL DEFAULT 0,
    xp INTEGER NOT NULL DEFAULT 0
);

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
