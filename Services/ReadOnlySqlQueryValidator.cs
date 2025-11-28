using System.Text.RegularExpressions;
using MsSqlMCP.Interfaces;

namespace MsSqlMCP.Services;

/// <summary>
/// Validates SQL queries to ensure only read-only (SELECT) operations are allowed.
/// This protects the database from modifications through the MCP server.
/// </summary>
public partial class ReadOnlySqlQueryValidator : ISqlQueryValidator
{
    /// <summary>
    /// SQL keywords that can modify data or schema - all blocked in read-only mode.
    /// </summary>
    private static readonly HashSet<string> BlockedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // DML statements
        "INSERT",
        "UPDATE", 
        "DELETE",
        "MERGE",
        "TRUNCATE",
        
        // DDL statements
        "DROP",
        "ALTER",
        "CREATE",
        
        // DCL statements
        "GRANT",
        "REVOKE",
        "DENY",
        
        // Execution statements
        "EXEC",
        "EXECUTE",
        "SP_EXECUTESQL",
        
        // Bulk operations
        "BULK",
        "OPENROWSET",
        "OPENDATASOURCE",
        
        // Backup/Restore
        "BACKUP",
        "RESTORE",
        
        // Other dangerous operations
        "SHUTDOWN",
        "KILL",
        "RECONFIGURE",
        "DBCC"
    };

    /// <inheritdoc />
    public ValidationResult Validate(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            return new ValidationResult(false, "SQL query cannot be empty.");
        }

        var normalizedQuery = sqlQuery.Trim();

        // Check that query starts with allowed read operations
        if (!IsReadOnlyQueryStart(normalizedQuery))
        {
            return new ValidationResult(false, 
                "Only SELECT queries are allowed. This is a read-only MCP server. " +
                "Queries must start with SELECT or WITH (for CTEs).");
        }

        // Check for blocked keywords anywhere in the query
        foreach (var keyword in BlockedKeywords)
        {
            if (ContainsKeywordAsWord(normalizedQuery, keyword))
            {
                return new ValidationResult(false, 
                    $"Error: {keyword} statements are not allowed. This is a read-only MCP server.");
            }
        }

        // Check for multiple statements (semicolon followed by another statement)
        if (ContainsMultipleStatements(normalizedQuery))
        {
            return new ValidationResult(false, 
                "Multiple SQL statements are not allowed. Please execute one SELECT query at a time.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Checks if the query starts with allowed read-only operations.
    /// </summary>
    private static bool IsReadOnlyQueryStart(string query)
    {
        return query.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
               query.StartsWith("WITH", StringComparison.OrdinalIgnoreCase) ||   // CTEs
               query.StartsWith("SET", StringComparison.OrdinalIgnoreCase) ||    // SET statements for session config
               query.StartsWith("--", StringComparison.Ordinal) ||               // Comments
               query.StartsWith("/*", StringComparison.Ordinal);                 // Block comments
    }

    /// <summary>
    /// Checks if a keyword appears as a complete word in the query.
    /// Uses word boundaries to avoid false positives (e.g., "UPDATED_DATE" shouldn't match "UPDATE").
    /// </summary>
    private static bool ContainsKeywordAsWord(string query, string keyword)
    {
        var pattern = $@"\b{Regex.Escape(keyword)}\b";
        return Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Detects if query contains multiple statements separated by semicolons.
    /// </summary>
    private static bool ContainsMultipleStatements(string query)
    {
        // Look for semicolon followed by another statement keyword
        var dangerousPatterns = new[]
        {
            @";\s*(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|EXEC|EXECUTE|TRUNCATE|GRANT|REVOKE|DENY)\b",
            @";\s*--.*\r?\n\s*(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|EXEC|EXECUTE)\b"
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (Regex.IsMatch(query, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                return true;
            }
        }

        return false;
    }
}
