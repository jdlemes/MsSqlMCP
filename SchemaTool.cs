using System.ComponentModel;
using ModelContextProtocol.Server;
using MsSqlMCP.Interfaces;

namespace MsSqlMCP;

/// <summary>
/// MCP Server tools for querying SQL Server database schema and executing read-only queries.
/// All methods use dependency injection for better testability and separation of concerns.
/// </summary>
[McpServerToolType]
public class SchemaTool
{
    private readonly ISchemaRepository _schemaRepository;
    private readonly IQueryExecutor _queryExecutor;

    public SchemaTool(ISchemaRepository schemaRepository, IQueryExecutor queryExecutor)
    {
        _schemaRepository = schemaRepository;
        _queryExecutor = queryExecutor;
    }

    [McpServerTool, Description("Get tables name of database. Optionally, specify a database name to query a different database in the same instance.")]
    public async Task<string> GetTables(string? databaseName = null)
    {
        var tables = await _schemaRepository.GetTablesAsync(databaseName);
        var dbInfo = FormatDatabaseInfo(databaseName);
        return $"Tables{dbInfo}:\n\n{string.Join("\n", tables)}";
    }

    [McpServerTool, Description("Get the columns (fields) of a database table. Optionally, specify a database name to query a different database in the same instance.")]
    public async Task<string> GetColumns(string tableName, string? databaseName = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return "Please specify the table name to query its fields.";
        }

        // Extract table name if it contains extra text
        var cleanTableName = ExtractTableName(tableName);
        if (string.IsNullOrEmpty(cleanTableName))
        {
            return "Please specify the table name to query its fields.";
        }

        var columns = await _schemaRepository.GetColumnsAsync(cleanTableName, databaseName);
        var dbInfo = FormatDatabaseInfo(databaseName);
        
        if (columns.Count == 0)
        {
            return $"No columns found for table '{cleanTableName}'{dbInfo}. Verify the table name is correct.";
        }
        
        return $"Columns in the table {cleanTableName}{dbInfo}:\n\n{string.Join("\n", columns)}";
    }

    [McpServerTool, Description("Get the relationships between tables in the database. Optionally, specify a database name to query a different database in the same instance.")]
    public async Task<string> GetRelationships(string? databaseName = null)
    {
        var relationships = await _schemaRepository.GetRelationshipsAsync(databaseName);
        var dbInfo = FormatDatabaseInfo(databaseName);
        
        if (relationships.Count == 0)
        {
            return $"No foreign key relationships found{dbInfo}.";
        }
        
        return $"Relationships between tables{dbInfo}:\n\n{string.Join("\n", relationships)}";
    }

    [McpServerTool, Description("Execute a read-only SQL query (SELECT only). INSERT, UPDATE, DELETE and other modifying statements are blocked for security. Optionally, specify a database name to query a different database in the same instance.")]
    public async Task<string> ExecuteSql(string sqlQuery, string? databaseName = null)
    {
        return await _queryExecutor.ExecuteReadOnlyQueryAsync(sqlQuery, databaseName);
    }

    [McpServerTool, Description("Get the definition of a stored procedure by name. Optionally, specify a database name to query a different database in the same instance.")]
    public async Task<string> GetStoreProcedure(string spName, string? databaseName = null)
    {
        if (string.IsNullOrWhiteSpace(spName))
        {
            return "Stored procedure name cannot be empty.";
        }

        var procedures = await _schemaRepository.GetStoredProcedureDefinitionAsync(spName, databaseName);
        var dbInfo = FormatDatabaseInfo(databaseName);
        
        if (procedures.Count == 0)
        {
            return $"Stored procedure '{spName}' not found{dbInfo}.";
        }
        
        return $"Stored procedure '{spName}'{dbInfo}:\n\n{string.Join("\n\n", procedures)}";
    }

    /// <summary>
    /// Formats database name for display in output messages.
    /// </summary>
    private static string FormatDatabaseInfo(string? databaseName) =>
        string.IsNullOrWhiteSpace(databaseName) ? string.Empty : $" (database '{databaseName}')";

    /// <summary>
    /// Extracts a clean table name from input that may contain additional text.
    /// Handles cases like "table Users" or "tabla Customers".
    /// </summary>
    private static string ExtractTableName(string input)
    {
        var words = input.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i].ToLowerInvariant();
            
            // If we find "table" or "tabla" keyword, return the next word
            if ((word == "tabla" || word == "table") && i + 1 < words.Length)
            {
                return CleanTableName(words[i + 1]);
            }
        }
        
        // If no keyword found, return the first word (assumed to be the table name)
        return words.Length > 0 ? CleanTableName(words[0]) : string.Empty;
    }

    /// <summary>
    /// Removes common punctuation from table names.
    /// </summary>
    private static string CleanTableName(string name) =>
        name.Trim(',', '.', ':', ';', '?', '!', '"', '\'', '[', ']');
}
