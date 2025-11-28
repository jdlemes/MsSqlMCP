using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MsSqlMCP.Interfaces;

namespace MsSqlMCP.Services;

/// <summary>
/// Executes validated read-only SQL queries against the database.
/// </summary>
public class SafeQueryExecutor : IQueryExecutor
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ISqlQueryValidator _validator;
    private readonly ILogger<SafeQueryExecutor> _logger;

    public SafeQueryExecutor(
        IConnectionFactory connectionFactory, 
        ISqlQueryValidator validator,
        ILogger<SafeQueryExecutor> logger)
    {
        _connectionFactory = connectionFactory;
        _validator = validator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ExecuteReadOnlyQueryAsync(string sqlQuery, string? databaseName = null)
    {
        // Validate the query before execution
        var validationResult = _validator.Validate(sqlQuery);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Query validation failed: {ErrorMessage}", validationResult.ErrorMessage);
            return validationResult.ErrorMessage!;
        }

        _logger.LogDebug("Executing validated query on database: {DatabaseName}", databaseName ?? "default");

        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(databaseName);
            
            var resultBuilder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                resultBuilder.AppendLine($"Database: {databaseName}");
                resultBuilder.AppendLine();
            }

            using var command = new SqlCommand(sqlQuery, connection);
            using var reader = await command.ExecuteReaderAsync();

            if (reader.HasRows)
            {
                // Build header
                var headers = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    headers.Add(reader.GetName(i));
                }
                resultBuilder.AppendLine(string.Join("\t|\t", headers));
                resultBuilder.AppendLine(new string('-', Math.Min(headers.Count * 20, 120)));

                // Build rows
                var rowCount = 0;
                while (await reader.ReadAsync())
                {
                    var values = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        values.Add(reader.IsDBNull(i) ? "NULL" : reader[i]?.ToString() ?? "NULL");
                    }
                    resultBuilder.AppendLine(string.Join("\t|\t", values));
                    rowCount++;
                }

                resultBuilder.AppendLine();
                resultBuilder.AppendLine($"({rowCount} row(s) returned)");
            }
            else
            {
                resultBuilder.AppendLine("No rows returned from the query.");
            }

            _logger.LogDebug("Query executed successfully");
            return resultBuilder.ToString();
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error executing query");
            return $"SQL Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");
            return $"Error executing SQL query: {ex.Message}";
        }
    }
}
