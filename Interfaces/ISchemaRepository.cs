namespace MsSqlMCP.Interfaces;

/// <summary>
/// Repository for querying database schema information.
/// </summary>
public interface ISchemaRepository
{
    /// <summary>
    /// Gets all table names in the database.
    /// </summary>
    Task<IReadOnlyList<string>> GetTablesAsync(string? databaseName = null);

    /// <summary>
    /// Gets column information for a specific table.
    /// </summary>
    Task<IReadOnlyList<string>> GetColumnsAsync(string tableName, string? databaseName = null);

    /// <summary>
    /// Gets all foreign key relationships in the database.
    /// </summary>
    Task<IReadOnlyList<string>> GetRelationshipsAsync(string? databaseName = null);

    /// <summary>
    /// Gets the definition of a stored procedure.
    /// </summary>
    Task<IReadOnlyList<string>> GetStoredProcedureDefinitionAsync(string spName, string? databaseName = null);
}
