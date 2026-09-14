using Dapper;
using Npgsql;
using TrucoUruguayo.Bot.Modelo;

namespace TrucoUruguayo.Bot.Datos;

public enum ResultadoCompra
{
    Exito,
    SinFondos,
    YaComprado,
    ItemNoExiste,
}

public class TiendaRepository
{
    private readonly string _connectionString;

    public TiendaRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<ItemTienda>> ObtenerItemsTiendaAsync()
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            SELECT id AS Id, nombre AS Nombre, descripcion AS Descripcion, precio AS Precio
            FROM tienda_items
            WHERE activo = TRUE
            ORDER BY id
            """;

        return await conexion.QueryAsync<ItemTienda>(sql);
    }

    public async Task<IEnumerable<ItemInventario>> ObtenerInventarioAsync(ulong discordId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            SELECT inventario_usuarios.item_id AS ItemId,
                   tienda_items.nombre AS Nombre,
                   inventario_usuarios.fecha_compra AS FechaCompra,
                   inventario_usuarios.equipado AS Equipado
            FROM inventario_usuarios
            JOIN tienda_items ON tienda_items.id = inventario_usuarios.item_id
            WHERE inventario_usuarios.usuario_id = @Id
            ORDER BY inventario_usuarios.fecha_compra DESC
            """;

        return await conexion.QueryAsync<ItemInventario>(sql, new { Id = (long)discordId });
    }

    public async Task<bool?> AlternarEquipamientoAsync(ulong discordId, int itemId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);

        const string sql = """
            UPDATE inventario_usuarios
            SET equipado = NOT equipado
            WHERE usuario_id = @Id AND item_id = @ItemId
            RETURNING equipado
            """;

        return await conexion.QuerySingleOrDefaultAsync<bool?>(sql, new { Id = (long)discordId, ItemId = itemId });
    }

    public async Task<ResultadoCompra> ComprarItemAsync(ulong discordId, int itemId)
    {
        await using var conexion = new NpgsqlConnection(_connectionString);
        await conexion.OpenAsync();
        await using var transaccion = await conexion.BeginTransactionAsync();

        var id = (long)discordId;

        const string sqlItem = "SELECT precio FROM tienda_items WHERE id = @ItemId AND activo = TRUE";
        var precio = await conexion.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(sqlItem, new { ItemId = itemId }, transaccion));

        if (precio is null)
        {
            await transaccion.CommitAsync();
            return ResultadoCompra.ItemNoExiste;
        }

        const string sqlMonedas = "SELECT monedas FROM usuarios WHERE id = @Id FOR UPDATE";
        var monedas = await conexion.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(sqlMonedas, new { Id = id }, transaccion));

        if (monedas is null)
        {
            await transaccion.CommitAsync();
            return ResultadoCompra.SinFondos;
        }

        const string sqlYaComprado = "SELECT 1 FROM inventario_usuarios WHERE usuario_id = @Id AND item_id = @ItemId";
        var yaComprado = await conexion.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(sqlYaComprado, new { Id = id, ItemId = itemId }, transaccion));

        if (yaComprado is not null)
        {
            await transaccion.CommitAsync();
            return ResultadoCompra.YaComprado;
        }

        if (monedas.Value < precio.Value)
        {
            await transaccion.CommitAsync();
            return ResultadoCompra.SinFondos;
        }

        const string sqlDescontar = "UPDATE usuarios SET monedas = monedas - @Precio WHERE id = @Id";
        await conexion.ExecuteAsync(
            new CommandDefinition(sqlDescontar, new { Id = id, Precio = precio.Value }, transaccion));

        const string sqlInsertar = """
            INSERT INTO inventario_usuarios (usuario_id, item_id, fecha_compra)
            VALUES (@Id, @ItemId, @Fecha)
            """;
        await conexion.ExecuteAsync(new CommandDefinition(
            sqlInsertar, new { Id = id, ItemId = itemId, Fecha = DateTime.UtcNow }, transaccion));

        await transaccion.CommitAsync();
        return ResultadoCompra.Exito;
    }
}
