using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;

using System.Text.Json.Serialization;

namespace PersonelTakip.Services;

public class AppConfiguration
{
    public DatabaseConfig Database { get; set; } = new();
    public AppConfig App { get; set; } = new();

    private static AppConfiguration? _instance;
    public static AppConfiguration Instance => _instance ??= Load();

    public event EventHandler? ConfigurationChanged;

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        File.WriteAllText(path, json);
        TriggerConfigurationChanged();
    }

    public void TriggerConfigurationChanged()
    {
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private static AppConfiguration Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var config = new AppConfiguration();
        configuration.Bind(config);
        return config;
    }
}

public class DatabaseConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    [JsonIgnore]
    public string ConnectionString =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};SSL Mode=Require;Trust Server Certificate=true;Timeout=15;Connection Idle Lifetime=60;";
}


public class AppConfig
{
    public int TokenExpiryHours { get; set; } = 24;
    public int MinPasswordLength { get; set; } = 6;
}
