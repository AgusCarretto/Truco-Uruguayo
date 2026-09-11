using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TrucoUruguayo.Bot.Datos;

Env.Load();

var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN")
    ?? throw new InvalidOperationException("Falta DISCORD_TOKEN en el .env");
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Falta DB_CONNECTION_STRING en el .env");
var guildId = ulong.Parse(Environment.GetEnvironmentVariable("DISCORD_GUILD_ID")
    ?? throw new InvalidOperationException("Falta DISCORD_GUILD_ID en el .env"));

try
{
    await using var conexionDePrueba = new NpgsqlConnection(connectionString);
    await conexionDePrueba.OpenAsync();
    Console.WriteLine("Conexion a la base de datos OK.");
}
catch (Exception ex)
{
    Console.WriteLine($"No se pudo conectar a la base de datos: {ex.Message}");
}

var services = new ServiceCollection()
    .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds,
    }))
    .AddSingleton(provider => new InteractionService(provider.GetRequiredService<DiscordSocketClient>()))
    .AddSingleton(new UsuarioRepository(connectionString))
    .BuildServiceProvider();

var client = services.GetRequiredService<DiscordSocketClient>();
var interactions = services.GetRequiredService<InteractionService>();

client.Log += mensaje =>
{
    Console.WriteLine(mensaje.ToString());
    return Task.CompletedTask;
};

client.InteractionCreated += async interaction =>
{
    var context = new SocketInteractionContext(client, interaction);
    await interactions.ExecuteCommandAsync(context, services);
};

client.Ready += async () =>
{
    await interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), services);
    await interactions.RegisterCommandsToGuildAsync(guildId);
};

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

await Task.Delay(Timeout.Infinite);
