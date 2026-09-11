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
            SELECT id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas
            FROM usuarios
            WHERE id = @Id
            """;

        return await conexion.QuerySingleOrDefaultAsync<Usuario>(sql, new { Id = (long)discordId });
    }

    public async Task<Usuario> RegistrarUsuarioAsync(ulong discordId, string nombre)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            INSERT INTO usuarios (id, nombre, monedas, victorias, derrotas)
            VALUES (@Id, @Nombre, @Monedas, 0, 0)
            RETURNING id AS Id, nombre AS Nombre, monedas AS Monedas, victorias AS Victorias, derrotas AS Derrotas
            """;

        return await conexion.QuerySingleAsync<Usuario>(
            sql,
            new { Id = (long)discordId, Nombre = nombre, Monedas = MonedasIniciales });
    }
}
