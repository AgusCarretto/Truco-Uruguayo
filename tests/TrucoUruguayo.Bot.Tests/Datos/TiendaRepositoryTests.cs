using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Tests.Helpers;
using Xunit;

namespace TrucoUruguayo.Bot.Tests.Datos;

public class TiendaRepositoryTests
{
    private readonly string _connectionString = ConexionDePrueba.ObtenerConnectionString();

    [Fact]
    public async Task ComprarItemAsync_ConFondosSuficientes_DescuentaLasMonedasYAgregaElItem()
    {
        var repositorioTienda = new TiendaRepository(_connectionString);
        var repositorioUsuarios = new UsuarioRepository(_connectionString);

        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 1000);
        await using var item = await ItemDePrueba.CrearAsync(_connectionString, precio: 300);

        var resultado = await repositorioTienda.ComprarItemAsync(usuario.Id, item.Id);

        Assert.Equal(ResultadoCompra.Exito, resultado);

        var usuarioActualizado = await repositorioUsuarios.ObtenerUsuarioAsync(usuario.Id);
        Assert.NotNull(usuarioActualizado);
        Assert.Equal(700, usuarioActualizado!.Monedas);

        var inventario = await repositorioTienda.ObtenerInventarioAsync(usuario.Id);
        Assert.Contains(inventario, i => i.ItemId == item.Id);
    }

    [Fact]
    public async Task ComprarItemAsync_SinMonedasSuficientes_NoDescuentaNiAgregaElItem()
    {
        var repositorioTienda = new TiendaRepository(_connectionString);
        var repositorioUsuarios = new UsuarioRepository(_connectionString);

        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 100);
        await using var item = await ItemDePrueba.CrearAsync(_connectionString, precio: 300);

        var resultado = await repositorioTienda.ComprarItemAsync(usuario.Id, item.Id);

        Assert.Equal(ResultadoCompra.SinFondos, resultado);

        var usuarioActualizado = await repositorioUsuarios.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(100, usuarioActualizado!.Monedas);

        var inventario = await repositorioTienda.ObtenerInventarioAsync(usuario.Id);
        Assert.DoesNotContain(inventario, i => i.ItemId == item.Id);
    }

    [Fact]
    public async Task ComprarItemAsync_ItemQueNoExisteOInactivo_DevuelveItemNoExiste()
    {
        var repositorioTienda = new TiendaRepository(_connectionString);

        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 1000);

        var resultado = await repositorioTienda.ComprarItemAsync(usuario.Id, itemId: -1);

        Assert.Equal(ResultadoCompra.ItemNoExiste, resultado);
    }

    [Fact]
    public async Task ComprarItemAsync_ItemInactivo_DevuelveItemNoExiste()
    {
        var repositorioTienda = new TiendaRepository(_connectionString);

        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 1000);
        await using var item = await ItemDePrueba.CrearAsync(_connectionString, precio: 100, activo: false);

        var resultado = await repositorioTienda.ComprarItemAsync(usuario.Id, item.Id);

        Assert.Equal(ResultadoCompra.ItemNoExiste, resultado);
    }

    [Fact]
    public async Task ComprarItemAsync_ItemYaComprado_NoDescuentaMonedasDeNuevo()
    {
        var repositorioTienda = new TiendaRepository(_connectionString);
        var repositorioUsuarios = new UsuarioRepository(_connectionString);

        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 1000);
        await using var item = await ItemDePrueba.CrearAsync(_connectionString, precio: 300);

        var primeraCompra = await repositorioTienda.ComprarItemAsync(usuario.Id, item.Id);
        var segundaCompra = await repositorioTienda.ComprarItemAsync(usuario.Id, item.Id);

        Assert.Equal(ResultadoCompra.Exito, primeraCompra);
        Assert.Equal(ResultadoCompra.YaComprado, segundaCompra);

        var usuarioActualizado = await repositorioUsuarios.ObtenerUsuarioAsync(usuario.Id);
        Assert.Equal(700, usuarioActualizado!.Monedas);
    }

    [Fact]
    public async Task AlternarEquipamientoAsync_ItemPropio_CambiaDeEstadoCadaVez()
    {
        var repositorioTienda = new TiendaRepository(_connectionString);

        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 1000);
        await using var item = await ItemDePrueba.CrearAsync(_connectionString, precio: 100);
        await repositorioTienda.ComprarItemAsync(usuario.Id, item.Id);

        var primerToggle = await repositorioTienda.AlternarEquipamientoAsync(usuario.Id, item.Id);
        var segundoToggle = await repositorioTienda.AlternarEquipamientoAsync(usuario.Id, item.Id);

        Assert.True(primerToggle);
        Assert.False(segundoToggle);
    }

    [Fact]
    public async Task AlternarEquipamientoAsync_ItemQueNoPosee_DevuelveNull()
    {
        var repositorioTienda = new TiendaRepository(_connectionString);

        await using var usuario = await UsuarioDePrueba.CrearAsync(_connectionString, monedas: 1000);
        await using var item = await ItemDePrueba.CrearAsync(_connectionString, precio: 100);

        var resultado = await repositorioTienda.AlternarEquipamientoAsync(usuario.Id, item.Id);

        Assert.Null(resultado);
    }
}
