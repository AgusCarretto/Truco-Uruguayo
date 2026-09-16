using Dapper;
using Npgsql;
using TrucoUruguayo.Bot.Modelo;

namespace TrucoUruguayo.Bot.Datos;

public class UsuarioRepository
{
    private const int MonedasIniciales = 1000;

    private readonly string _connectionString;

    public UsuarioRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Usuario?> ObtenerUsuarioAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel, titulo_equipado AS TituloEquipado, mazo_equipado AS MazoEquipado
            FROM usuarios
            WHERE id = @Id
            """;

        return await conexion.QuerySingleOrDefaultAsync<Usuario>(sql, new { Id = (long)discordId });
    }

    public async Task<Usuario> RegistrarUsuarioAsync(ulong discordId, string nombre)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            INSERT INTO usuarios (id, nombre, monedas, victorias, derrotas, xp)
            VALUES (@Id, @Nombre, @Monedas, 0, 0, 0)
            RETURNING id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel, titulo_equipado AS TituloEquipado, mazo_equipado AS MazoEquipado
            """;

        return await conexion.QuerySingleAsync<Usuario>(
            sql,
            new { Id = (long)discordId, Nombre = nombre, Monedas = MonedasIniciales });
    }

    public async Task ActualizarMonedasAsync(ulong discordId, int delta)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = "UPDATE usuarios SET monedas = monedas + @Delta WHERE id = @Id";

        await conexion.ExecuteAsync(sql, new { Id = (long)discordId, Delta = delta });
    }

    public async Task SumarVictoriaAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = "UPDATE usuarios SET victorias = victorias + 1 WHERE id = @Id";

        await conexion.ExecuteAsync(sql, new { Id = (long)discordId });
    }

    public async Task SumarDerrotaAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = "UPDATE usuarios SET derrotas = derrotas + 1 WHERE id = @Id";

        await conexion.ExecuteAsync(sql, new { Id = (long)discordId });
    }

    public async Task<IEnumerable<Usuario>> ObtenerTopUsuariosAsync(string orden, int limite = 10)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        var columna = orden == "xp" ? "xp" : "monedas";

        var sql = $"""
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel, titulo_equipado AS TituloEquipado, mazo_equipado AS MazoEquipado
            FROM usuarios
            ORDER BY {columna} DESC
            LIMIT @Limite
            """;

        return await conexion.QueryAsync<Usuario>(sql, new { Limite = limite });
    }

    public async Task<(bool Exito, TimeSpan? TiempoRestante)> ReclamarDiariaAsync(ulong discordId)
    {
        const int MonedasDiarias = 500;

        await using var conexion = new NpgsqlConnection(_connectionString);
        await conexion.OpenAsync();
        await using var transaccion = await conexion.BeginTransactionAsync();

        var id = (long)discordId;
        var ahora = DateTime.UtcNow;

        const string sqlUltimoReclamo = """
            SELECT ultimo_reclamo
            FROM recompensas_diarias
            WHERE usuario_id = @Id
            FOR UPDATE
            """;

        var ultimoReclamo = await conexion.QuerySingleOrDefaultAsync<DateTime?>(
            new CommandDefinition(sqlUltimoReclamo, new { Id = id }, transaccion));

        if (ultimoReclamo is not null)
        {
            var tiempoTranscurrido = ahora - ultimoReclamo.Value;
            if (tiempoTranscurrido < TimeSpan.FromHours(24))
            {
                await transaccion.CommitAsync();
                return (false, TimeSpan.FromHours(24) - tiempoTranscurrido);
            }
        }

        const string sqlUpsert = """
            INSERT INTO recompensas_diarias (usuario_id, ultimo_reclamo)
            VALUES (@Id, @Ahora)
            ON CONFLICT (usuario_id) DO UPDATE SET ultimo_reclamo = @Ahora
            """;

        await conexion.ExecuteAsync(new CommandDefinition(sqlUpsert, new { Id = id, Ahora = ahora }, transaccion));

        const string sqlSumarMonedas = "UPDATE usuarios SET monedas = monedas + @Monedas WHERE id = @Id";

        await conexion.ExecuteAsync(
            new CommandDefinition(sqlSumarMonedas, new { Id = id, Monedas = MonedasDiarias }, transaccion));

        await transaccion.CommitAsync();
        return (true, null);
    }

    public async Task RegistrarPartidaAsync(ulong ganadorId, ulong perdedorId, int apuesta)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            INSERT INTO partidas_historico (ganador_id, perdedor_id, apuesta, fecha)
            VALUES (@GanadorId, @PerdedorId, @Apuesta, @Fecha)
            """;

        await conexion.ExecuteAsync(sql, new
        {
            GanadorId = (long)ganadorId,
            PerdedorId = (long)perdedorId,
            Apuesta = apuesta,
            Fecha = DateTime.UtcNow,
        });
    }

    public async Task<IEnumerable<PartidaHistorico>> ObtenerHistorialAsync(ulong discordId, int limite = 5)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            SELECT id AS Id, ganador_id AS GanadorId, perdedor_id AS PerdedorId, apuesta AS Apuesta, fecha AS Fecha
            FROM partidas_historico
            WHERE ganador_id = @Id OR perdedor_id = @Id
            ORDER BY fecha DESC
            LIMIT @Limite
            """;

        return await conexion.QueryAsync<PartidaHistorico>(sql, new { Id = (long)discordId, Limite = limite });
    }

    public async Task<(bool SubioDeNivel, int NuevoNivel)> SumarExpAsync(ulong discordId, int cantidadExp)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);
        await conexion.OpenAsync();
        await using var transaccion = await conexion.BeginTransactionAsync();

        var id = (long)discordId;

        const string sqlSeleccionar = """
            SELECT xp AS Xp, nivel AS Nivel
            FROM usuarios
            WHERE id = @Id
            FOR UPDATE
            """;

        var actual = await conexion.QuerySingleAsync<XpYNivel>(
            new CommandDefinition(sqlSeleccionar, new { Id = id }, transaccion));

        var nuevoXp = actual.Xp + cantidadExp;
        var nuevoNivel = actual.Nivel;

        while (nuevoXp >= NivelCalculadora.XpParaAlcanzarNivel(nuevoNivel + 1))
        {
            nuevoNivel++;
        }

        const string sqlActualizar = "UPDATE usuarios SET xp = @Xp, nivel = @Nivel WHERE id = @Id";

        await conexion.ExecuteAsync(
            new CommandDefinition(sqlActualizar, new { Xp = nuevoXp, Nivel = nuevoNivel, Id = id }, transaccion));

        await transaccion.CommitAsync();

        return (nuevoNivel > actual.Nivel, nuevoNivel);
    }

    private sealed class XpYNivel
    {
        public int Xp { get; set; }
        public int Nivel { get; set; }
    }

    public async Task<bool> EquiparTituloAsync(ulong discordId, string titulo)
    {
        if (!ConstantesTitulos.TitulosPorNivel.ContainsValue(titulo))
        {
            return false;
        }

        var usuario = await ObtenerUsuarioAsync(discordId);
        if (usuario is null)
        {
            return false;
        }

        var nivelRequerido = ConstantesTitulos.TitulosPorNivel.First(kv => kv.Value == titulo).Key;
        if (usuario.Nivel < nivelRequerido)
        {
            return false;
        }

        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = "UPDATE usuarios SET titulo_equipado = @Titulo WHERE id = @Id";

        await conexion.ExecuteAsync(sql, new { Titulo = titulo, Id = (long)discordId });

        return true;
    }

    private static readonly HashSet<string> MazosValidos = ["mazo_basico", "mazo_clasico"];
    private const string NombreItemMazoClasico = "Mazo Clásico";

    public async Task<bool> EquiparMazoAsync(ulong discordId, string mazo)
    {
        if (!MazosValidos.Contains(mazo))
        {
            return false;
        }

        if (mazo == "mazo_clasico")
        {
            await using var conexionCheck = new NpgsqlConnection(_connectionString);

            const string sqlPoseeMazo = """
                SELECT 1
                FROM inventario_usuarios
                JOIN tienda_items ON tienda_items.id = inventario_usuarios.item_id
                WHERE inventario_usuarios.usuario_id = @Id AND tienda_items.nombre = @Nombre
                """;

            var poseeMazo = await conexionCheck.QuerySingleOrDefaultAsync<int?>(
                sqlPoseeMazo, new { Id = (long)discordId, Nombre = NombreItemMazoClasico });

            if (poseeMazo is null)
            {
                return false;
            }
        }

        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sqlActualizar = "UPDATE usuarios SET mazo_equipado = @Mazo WHERE id = @Id";

        await conexion.ExecuteAsync(sqlActualizar, new { Mazo = mazo, Id = (long)discordId });

        return true;
    }
}
