using Npgsql;

namespace TrucoUruguayo.Bot.Tests.Helpers;

public sealed class UsuarioDePrueba : IAsyncDisposable
{
    private readonly string _connectionString;

    public ulong Id { get; }

    private UsuarioDePrueba(string connectionString, ulong id)
    {
        _connectionString = connectionString;
        Id = id;
    }

    public static ulong GenerarId() => (ulong)Random.Shared.NextInt64(1_000_000_000, 2_000_000_000);

    public static UsuarioDePrueba ParaLimpieza(string connectionString, ulong id) => new(connectionString, id);

    public static async Task<UsuarioDePrueba> CrearAsync(string connectionString, int monedas = 1000)
    {
        var id = GenerarId();

        await using var conexion = new NpgsqlConnection(connectionString);
        await conexion.OpenAsync();

        await using var comando = new NpgsqlCommand(
            "INSERT INTO usuarios (id, nombre, monedas, victorias, derrotas, xp) VALUES (@Id, @Nombre, @Monedas, 0, 0, 0)",
            conexion);
        comando.Parameters.AddWithValue("Id", (long)id);
        comando.Parameters.AddWithValue("Nombre", $"prueba-{id}");
        comando.Parameters.AddWithValue("Monedas", monedas);
        await comando.ExecuteNonQueryAsync();

        return new UsuarioDePrueba(connectionString, id);
    }

    public async ValueTask DisposeAsync()
    {
        await using var conexion = new NpgsqlConnection(_connectionString);
        await conexion.OpenAsync();

        await using var comando = new NpgsqlCommand(
            """
            DELETE FROM inventario_usuarios WHERE usuario_id = @Id;
            DELETE FROM recompensas_diarias WHERE usuario_id = @Id;
            DELETE FROM partidas_historico WHERE ganador_id = @Id OR perdedor_id = @Id;
            DELETE FROM usuarios WHERE id = @Id;
            """,
            conexion);
        comando.Parameters.AddWithValue("Id", (long)Id);
        await comando.ExecuteNonQueryAsync();
    }
}
