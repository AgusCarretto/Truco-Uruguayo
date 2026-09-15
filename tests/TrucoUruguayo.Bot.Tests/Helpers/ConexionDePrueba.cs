using DotNetEnv;

namespace TrucoUruguayo.Bot.Tests.Helpers;

public static class ConexionDePrueba
{
    public static string ObtenerConnectionString()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        string? envPath = null;

        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, "src", "TrucoUruguayo.Bot", ".env");
            if (File.Exists(candidato))
            {
                envPath = candidato;
                break;
            }

            directorio = directorio.Parent;
        }

        if (envPath is null)
        {
            throw new InvalidOperationException(
                "No se encontro src/TrucoUruguayo.Bot/.env. Estos tests necesitan una Postgres local con las tablas de schema.sql ya creadas.");
        }

        Env.Load(envPath);

        return Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Falta DB_CONNECTION_STRING en el .env");
    }
}
