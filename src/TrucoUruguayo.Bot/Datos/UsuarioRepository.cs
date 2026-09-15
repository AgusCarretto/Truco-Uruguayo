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
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel
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
            RETURNING id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel
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
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas, xp AS Xp, nivel AS Nivel
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
}
