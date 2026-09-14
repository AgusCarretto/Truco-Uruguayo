using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TrucoUruguayo.Bot.Datos;
using TrucoUruguayo.Bot.Servicios;

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
    .AddSingleton(new TiendaRepository(connectionString))
    .AddSingleton<GestorPartidas>()
    .AddSingleton<GeneradorImagenes>()
    .BuildServiceProvider();

var client = services.GetRequiredService<DiscordSocketClient>();
var interactions = services.GetRequiredService<InteractionService>();
var usuarioRepository = services.GetRequiredService<UsuarioRepository>();

client.Log += mensaje =>
{
    Console.WriteLine(mensaje.ToString());
    return Task.CompletedTask;
};

interactions.Log += mensaje =>
{
    Console.WriteLine(mensaje.ToString());
    return Task.CompletedTask;
};

client.InteractionCreated += async interaction =>
{
    try
    {
        // Garantiza que todo SlashCommand handler (ej. PerfilModule) vea siempre un usuario ya registrado.
        if (interaction is SocketSlashCommand
            && await usuarioRepository.ObtenerUsuarioAsync(interaction.User.Id) is null)
        {
            await usuarioRepository.RegistrarUsuarioAsync(interaction.User.Id, interaction.User.Username);
            await EnviarBienvenidaAsync(interaction);
            return;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error en el chequeo de usuario nuevo: {ex.Message}");
        if (!interaction.HasResponded)
        {
            await interaction.RespondAsync("Hubo un problema, probá de nuevo en un rato.", ephemeral: true);
        }
        return;
    }

    var context = new SocketInteractionContext(client, interaction);
    var resultado = await interactions.ExecuteCommandAsync(context, services);

    if (!resultado.IsSuccess && !interaction.HasResponded)
    {
        await interaction.RespondAsync("😵 Hubo un problema al procesar eso, probá de nuevo en un rato.", ephemeral: true);
    }
};

client.Ready += async () =>
{
    await interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), services);
    await interactions.RegisterCommandsToGuildAsync(guildId);
};

async Task EnviarBienvenidaAsync(SocketInteraction interaction)
{
    var embed = new EmbedBuilder()
        .WithTitle("🎉 ¡Bienvenido a Truco Uruguayo!")
        .WithDescription("Te registramos y te regalamos 1000 monedas para arrancar. Tirá tu comando de nuevo cuando quieras.")
        .WithColor(Color.Gold)
        .Build();

    var componentes = new ComponentBuilder()
        .WithButton("❓ ¿Cómo se juega?", "como_jugar", ButtonStyle.Primary)
        .Build();

    await interaction.RespondAsync(embed: embed, components: componentes, ephemeral: true);
}

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

await Task.Delay(Timeout.Infinite);
