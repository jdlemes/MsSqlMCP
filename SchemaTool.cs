using System.ComponentModel;
using MsSqlMCP.Helpers;
using ModelContextProtocol.Server;
using Microsoft.Data.SqlClient;
using MsSqlMCP;

[McpServerToolType]
public static class SchemaTool
{

    [McpServerTool, Description("Get tables name of database")]
    public async static Task<string> GetTables()
    {
        var (connectionString, warning) = Program.GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        SchemaHelper schemaHelper = new SchemaHelper();
        var tables = await schemaHelper.GetTablesAsync(connection);
        return $"Tables in the database:\n\n{string.Join("\n", tables)}";

    }
    [McpServerTool, Description("Get the columns (fields) of a database table")]
    public async static Task<string> GetColumns(string tableName)
    {
        var (connectionString, warning) = Program.GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        SchemaHelper schemaHelper = new SchemaHelper();
        // Extract the table name from the query
        tableName = schemaHelper.ExtractTableName(tableName);
        if (!string.IsNullOrEmpty(tableName))
        {
            var columns = await schemaHelper.GetColumnsAsync(connection, tableName);
            return $"Columns in the table {tableName}:\n\n{string.Join("\n", columns)}";
        }
        else
        {
            return "Please specify the table name to query its fields.";
        }
    }

    [McpServerTool, Description("Get the relationships between tables in the database")]
    public async static Task<string> GetRelationships()
    {
        var (connectionString, warning) = Program.GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        SchemaHelper schemaHelper = new SchemaHelper();

        var relationships = await schemaHelper.GetRelationshipsAsync(connection);
            return $"Relationships between tables:\n\n{string.Join("\n", relationships)}";
    }

}
