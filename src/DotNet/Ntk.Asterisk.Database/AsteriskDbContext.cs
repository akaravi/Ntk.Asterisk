using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ntk.Asterisk.Domain.Recording;

namespace Ntk.Asterisk.Database;

public sealed class AsteriskDbContext(DbContextOptions<AsteriskDbContext> options) : DbContext(options)
{
    public DbSet<AudioRecording> AudioRecordings => Set<AudioRecording>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AudioRecording>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Format).HasConversion<string>();
            entity.Property(x => x.Direction).HasConversion<string>();
        });
    }
}
public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddAsteriskDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"];
        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase))
            return services;
        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var name = configuration["Database:DatabaseName"];
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Database:DatabaseName is required for InMemory provider.");
            services.AddDbContext<AsteriskDbContext>(options => options.UseInMemoryDatabase(name));
            return services;
        }
        if (!string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported Database:Provider '{provider}'.");
        var connectionString = configuration.GetConnectionString("Asterisk");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("ConnectionStrings:Asterisk is required for SqlServer provider.");
        services.AddDbContext<AsteriskDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
