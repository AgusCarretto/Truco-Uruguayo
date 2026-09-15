using Npgsql;

namespace TrucoUruguayo.Bot.Tests.Helpers;

public sealed class ItemDePrueba : IAsyncDisposable
{
    private readonly string _connectionString;

    public int Id { get; }
    public int Precio { get; }

    private ItemDePrueba(string connectionString, int id, int precio)
    {
        _connectionString = connectionString;
        Id = id;
        Precio = precio;
    }

    public static async Task<ItemDePrueba> CrearAsync(string connectionString, int precio = 100, bool activo = true)
    {
        await using var conexion = new NpgsqlConnection(connectionString);
        await conexion.OpenAsync();

        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO tienda_items (nombre, descripcion, precio, activo)
            VALUES (@Nombre, @Descripcion, @Precio, @Activo)
            RETURNING id
            """,
            conexion);
        comando.Parameters.AddWithValue("Nombre", $"Item de prueba {Guid.NewGuid()}");
        comando.Parameters.AddWithValue("Descripcion", "Item creado por los tests");
        comando.Parameters.AddWithValue("Precio", precio);
        comando.Parameters.AddWithValue("Activo", activo);

        var id = (int)(await comando.ExecuteScalarAsync())!;

        return new ItemDePrueba(connectionString, id, precio);
    }

    public async ValueTask DisposeAsync()
    {
        await using var conexion = new NpgsqlConnection(_connectionString);
        await conexion.OpenAsync();

        await using var comando = new NpgsqlCommand(
            """
            DELETE FROM inventario_usuarios WHERE item_id = @Id;
            DELETE FROM tienda_items WHERE id = @Id;
            """,
            conexion);
        comando.Parameters.AddWithValue("Id", Id);
        await comando.ExecuteNonQueryAsync();
    }
}
