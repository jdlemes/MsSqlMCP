using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MsSqlMCP.Interfaces;

namespace MsSqlMCP.Services;

/// <summary>
/// Repository implementation for querying SQL Server schema information.
/// </summary>
public class SchemaRepository : ISchemaRepository
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<SchemaRepository> _logger;

    public SchemaRepository(IConnectionFactory connectionFactory, ILogger<SchemaRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetTablesAsync(string? databaseName = null)
    {
        _logger.LogDebug("Getting tables list for database: {DatabaseName}", databaseName ?? "default");

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(databaseName);

        const string sql = @"
            SELECT TABLE_SCHEMA, TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var tableName = reader.GetString(1);
            tables.Add($"{schema}.{tableName}");
        }

        _logger.LogDebug("Found {Count} tables", tables.Count);
        return tables;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetColumnsAsync(string tableName, string? databaseName = null)
    {
        var (schema, table) = ParseTableName(tableName);
        _logger.LogDebug("Getting columns for table: {Schema}.{Table}", schema, table);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(databaseName);

        const string sql = @"
            SELECT 
                COLUMN_NAME, 
                DATA_TYPE, 
                CHARACTER_MAXIMUM_LENGTH,
                IS_NULLABLE,
                COLUMNPROPERTY(object_id(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') as IS_IDENTITY,
                (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                    JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc 
                    ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                    WHERE kcu.TABLE_SCHEMA = c.TABLE_SCHEMA
                    AND kcu.TABLE_NAME = c.TABLE_NAME
                    AND kcu.COLUMN_NAME = c.COLUMN_NAME
                    AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) as IS_PRIMARY_KEY
            FROM 
                INFORMATION_SCHEMA.COLUMNS c
            WHERE 
                TABLE_SCHEMA = @schema 
                AND TABLE_NAME = @tableName
            ORDER BY 
                ORDINAL_POSITION";

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@tableName", table);

        using var reader = await command.ExecuteReaderAsync();

        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var charMaxLength = reader.IsDBNull(2) ? null : reader.GetValue(2)?.ToString();
            var isNullable = reader.GetString(3);
            var isIdentity = reader.GetInt32(4);
            var isPrimaryKey = reader.GetInt32(5);

            var lengthInfo = charMaxLength != null ? $"({charMaxLength})" : "";
            var nullableInfo = isNullable == "YES" ? "NULL" : "NOT NULL";
            var identityInfo = isIdentity == 1 ? " IDENTITY" : "";
            var pkInfo = isPrimaryKey > 0 ? " PRIMARY KEY" : "";

            columns.Add($"{columnName} | {dataType}{lengthInfo} | {nullableInfo}{identityInfo}{pkInfo}");
        }

        _logger.LogDebug("Found {Count} columns for table {Table}", columns.Count, tableName);
        return columns;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRelationshipsAsync(string? databaseName = null)
    {
        _logger.LogDebug("Getting relationships for database: {DatabaseName}", databaseName ?? "default");

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(databaseName);

        const string sql = @"
            SELECT 
                fk.name AS ForeignKey,
                OBJECT_NAME(fk.parent_object_id) AS TableName,
                COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ColumnName,
                OBJECT_NAME(fk.referenced_object_id) AS ReferencedTableName,
                COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumnName
            FROM 
                sys.foreign_keys AS fk
            INNER JOIN 
                sys.foreign_key_columns AS fkc 
                ON fk.OBJECT_ID = fkc.constraint_object_id
            ORDER BY
                TableName, ReferencedTableName";

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        var relationships = new List<string>();
        while (await reader.ReadAsync())
        {
            var foreignKey = reader.GetString(0);
            var tableName = reader.GetString(1);
            var columnName = reader.GetString(2);
            var referencedTableName = reader.GetString(3);
            var referencedColumnName = reader.GetString(4);

            relationships.Add($"{tableName}.{columnName} -> {referencedTableName}.{referencedColumnName} (FK: {foreignKey})");
        }

        _logger.LogDebug("Found {Count} relationships", relationships.Count);
        return relationships;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetStoredProcedureDefinitionAsync(string spName, string? databaseName = null)
    {
        _logger.LogDebug("Getting stored procedure definition: {SpName}", spName);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(databaseName);

        const string sql = @"
            SELECT name, object_definition(object_id) 
            FROM sys.procedures
            WHERE name = @spName";

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@spName", spName);
        using var reader = await command.ExecuteReaderAsync();

        var procedures = new List<string>();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            var definition = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            procedures.Add($"{name}:\n{definition}");
        }

        _logger.LogDebug("Found {Count} procedures matching name {SpName}", procedures.Count, spName);
        return procedures;
    }

    /// <summary>
    /// Parses a table name that may include schema prefix.
    /// </summary>
    private static (string Schema, string Table) ParseTableName(string tableName)
    {
        if (tableName.Contains('.'))
        {
            var parts = tableName.Split('.', 2);
            return (parts[0], parts[1]);
        }
        return ("dbo", tableName);
    }
}
