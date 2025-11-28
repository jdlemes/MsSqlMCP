using Microsoft.Data.SqlClient;

namespace MsSqlMCP.Interfaces;

/// <summary>
/// Factory for creating and managing SQL Server connections.
/// </summary>
public interface IConnectionFactory
{
    /// <summary>
    /// Creates and opens a new SQL connection, optionally switching to a specific database.
    /// </summary>
    /// <param name="databaseName">Optional database name to switch to after connecting.</param>
    /// <returns>An open SqlConnection ready to use.</returns>
    Task<SqlConnection> CreateOpenConnectionAsync(string? databaseName = null);
}
