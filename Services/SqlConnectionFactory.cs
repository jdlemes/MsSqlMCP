using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MsSqlMCP.Interfaces;

namespace MsSqlMCP.Services;

/// <summary>
/// Factory for creating SQL Server connections with optional database switching.
/// </summary>
public class SqlConnectionFactory : IConnectionFactory
{
    private readonly string _connectionString;
    private readonly ILogger<SqlConnectionFactory> _logger;

    public SqlConnectionFactory(IConfiguration configuration, ILogger<SqlConnectionFactory> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SqlConnection> CreateOpenConnectionAsync(string? databaseName = null)
    {
        var connection = new SqlConnection(_connectionString);
        
        try
        {
            await connection.OpenAsync();
            _logger.LogDebug("SQL connection opened successfully");

            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                var sanitizedName = SanitizeDatabaseName(databaseName);
                using var cmd = new SqlCommand($"USE {sanitizedName};", connection);
                await cmd.ExecuteNonQueryAsync();
                _logger.LogDebug("Switched to database {DatabaseName}", databaseName);
            }

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open SQL connection");
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Sanitizes database name to prevent SQL injection.
    /// Wraps in brackets and escapes internal bracket characters.
    /// </summary>
    private static string SanitizeDatabaseName(string name) => $"[{name.Replace("]", "]]")}]";
}
