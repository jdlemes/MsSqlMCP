using System.ComponentModel;
using MsSqlMCP.Helpers;
using ModelContextProtocol.Server;
using Microsoft.Data.SqlClient;
using MsSqlMCP;
using System.Text;

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

    [McpServerTool, Description("Execute a SQL query against the database. Does not allow DROP statements.")]
    public async static Task<string> ExecuteSql(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            return "SQL query cannot be empty.";
        }

        // Validate against DROP statements (case-insensitive)
        if (sqlQuery.Trim().ToUpperInvariant().StartsWith("DROP "))
        {
            return "Error: DROP statements are not allowed.";
        }

        var (connectionString, warning) = Program.GetConnectionString();
        if (warning)
        {
            // Potentially log the warning or handle it as needed
        }

        StringBuilder resultBuilder = new StringBuilder();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand(sqlQuery, connection);

            // Distinguish between queries that return data (SELECT) and those that don't (INSERT, UPDATE, DELETE)
            bool isSelectQuery = sqlQuery.Trim().ToUpperInvariant().StartsWith("SELECT");

            if (isSelectQuery)
            {
                using var reader = await command.ExecuteReaderAsync();
                if (reader.HasRows)
                {
                    // Column Headers
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        resultBuilder.Append(reader.GetName(i));
                        if (i < reader.FieldCount - 1) resultBuilder.Append("\t|\t");
                    }
                    resultBuilder.AppendLine();
                    resultBuilder.AppendLine(new string('-', resultBuilder.Length > 1 ? resultBuilder.Length -2 : 0)); // Separator line, ensure not negative

                    // Data Rows
                    while (await reader.ReadAsync())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            resultBuilder.Append(reader[i]?.ToString() ?? "NULL");
                            if (i < reader.FieldCount - 1) resultBuilder.Append("\t|\t");
                        }
                        resultBuilder.AppendLine();
                    }
                }
                else
                {
                    resultBuilder.AppendLine("No rows returned from the query.");
                }
            }
            else // For INSERT, UPDATE, DELETE, etc.
            {
                int affectedRows = await command.ExecuteNonQueryAsync();
                resultBuilder.AppendLine($"Command executed successfully. {affectedRows} row(s) affected.");
            }
        }
        catch (SqlException ex)
        {
            return $"SQL Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error executing SQL query: {ex.Message}";
        }

        return resultBuilder.ToString();
    }
}
