namespace MsSqlMCP.Interfaces;

/// <summary>
/// Validates SQL queries to ensure they are safe to execute.
/// </summary>
public interface ISqlQueryValidator
{
    /// <summary>
    /// Validates a SQL query and returns the result.
    /// </summary>
    /// <param name="sqlQuery">The SQL query to validate.</param>
    /// <returns>A ValidationResult indicating if the query is valid and any error message.</returns>
    ValidationResult Validate(string sqlQuery);
}

/// <summary>
/// Represents the result of a SQL query validation.
/// </summary>
/// <param name="IsValid">Whether the query passed validation.</param>
/// <param name="ErrorMessage">Error message if validation failed, null otherwise.</param>
public record ValidationResult(bool IsValid, string? ErrorMessage = null);
