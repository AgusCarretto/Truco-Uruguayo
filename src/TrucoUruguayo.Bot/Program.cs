using System.Collections.Concurrent;
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

// DISCORD_GUILD_ID es opcional: si esta seteado, los comandos se registran solo en ese
// servidor (cambios visibles al instante, ideal para desarrollo). Si no esta, se registran
// globalmente (tardan hasta ~1 hora en propagarse a todos los servidores, pero es lo que
// hace falta para que el bot funcione en cualquier servidor donde lo agreguen).
var guildIdTexto = Environment.GetEnvironmentVariable("DISCORD_GUILD_ID");
var guildId = string.IsNullOrWhiteSpace(guildIdTexto) ? (ulong?)null : ulong.Parse(guildIdTexto);

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

// Discord.Net puede despachar varias interacciones del mismo canal en paralelo (dos clicks
// rapidos, un reintento por lag, etc.). Ronda no tiene ningun lock propio, asi que sin esto
// dos jugadas simultaneas podrian pisarse y corromper el estado de la partida. Un semaforo
// por canal serializa todo lo que toca ese canal sin bloquear el resto del bot.
var semaforosPorCanal = new ConcurrentDictionary<ulong, SemaphoreSlim>();

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

    var semaforo = semaforosPorCanal.GetOrAdd(interaction.ChannelId ?? interaction.User.Id, _ => new SemaphoreSlim(1, 1));
    await semaforo.WaitAsync();
    try
    {
        var context = new SocketInteractionContext(client, interaction);
        var resultado = await interactions.ExecuteCommandAsync(context, services);

        if (!resultado.IsSuccess && !interaction.HasResponded)
        {
            await interaction.RespondAsync("😵 Hubo un problema al procesar eso, probá de nuevo en un rato.", ephemeral: true);
        }
    }
    finally
    {
        semaforo.Release();
    }
};

var modulosCargados = false;

client.Ready += async () =>
{
    // Ready se dispara de nuevo cada vez que el Gateway reconecta (no solo al arrancar),
    // asi que sin esta guarda AddModulesAsync intenta registrar los mismos modulos dos
    // veces y explota con "SlashCommandInfo already exists".
    if (modulosCargados)
    {
        return;
    }

    modulosCargados = true;
    await interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), services);

    if (guildId is not null)
    {
        await interactions.RegisterCommandsToGuildAsync(guildId.Value);
        Console.WriteLine($"Comandos registrados en el servidor {guildId.Value} (modo desarrollo).");
    }
    else
    {
        await interactions.RegisterCommandsGloballyAsync();
        Console.WriteLine("Comandos registrados globalmente. Puede tardar hasta 1 hora en aparecer en todos los servidores.");
    }

    await client.SetGameAsync("/truco para jugar | /ayuda", type: ActivityType.Playing);
};

client.JoinedGuild += async guild =>
{
    try
    {
        var canal = guild.SystemChannel ?? guild.DefaultChannel;
        if (canal is null || !guild.CurrentUser.GetPermissions(canal).SendMessages)
        {
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🎉 ¡Gracias por sumar Truco Uruguayo!")
            .WithDescription("Para arrancar: `/truco @alguien` para desafiar a jugar, o `/ayuda` para ver las reglas y todos los comandos.")
            .WithColor(Color.Gold)
            .Build();

        await canal.SendMessageAsync(embed: embed);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo mandar el mensaje de bienvenida en {guild.Name}: {ex.Message}");
    }
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
