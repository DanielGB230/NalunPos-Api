using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Pos.Infrastructure.Persistence.Context;

/// <summary>
/// Fábrica de DbContext a tiempo de diseño para la CLI de EF Core (dotnet ef migrations / database).
/// Lee la cadena de conexión de forma dinámica desde los User Secrets o appsettings del proyecto de arranque (Pos.Api).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        string currentDir = Directory.GetCurrentDirectory();

        // Determinar la ruta del proyecto de arranque Pos.Api
        string apiDirectory = currentDir;
        if (!File.Exists(Path.Combine(apiDirectory, "Pos.Api.csproj")))
        {
            string candidateSrc = Path.Combine(currentDir, "src", "Pos.Api");
            if (Directory.Exists(candidateSrc))
            {
                apiDirectory = candidateSrc;
            }
            else
            {
                string candidateParent = Path.Combine(currentDir, "..", "Pos.Api");
                if (Directory.Exists(candidateParent))
                {
                    apiDirectory = Path.GetFullPath(candidateParent);
                }
            }
        }

        var builder = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddUserSecrets("f8cd60cc-c652-42cf-8ad9-07514cac47b3")
            .AddEnvironmentVariables();

        IConfiguration configuration = builder.Build();

        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        // Si la cadena de conexión leída es el placeholder de desarrollo o está vacía, usar el fallback seguro de desarrollo
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Equals("Ver_UserSecrets_En_Desarrollo", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = "Server=(localdb)\\mssqllocaldb;Database=NalunPosDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options, null, null);
    }
}
