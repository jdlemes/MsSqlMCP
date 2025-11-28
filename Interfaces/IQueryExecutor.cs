namespace MsSqlMCP.Interfaces;

/// <summary>
/// Executes validated SQL queries against the database.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Executes a read-only SQL query and returns formatted results.
    /// </summary>
    /// <param name="sqlQuery">The SQL query to execute (must be SELECT-only).</param>
    /// <param name="databaseName">Optional database name to query.</param>
    /// <returns>Formatted query results as a string.</returns>
    Task<string> ExecuteReadOnlyQueryAsync(string sqlQuery, string? databaseName = null);
}
