using System.ComponentModel;
using MsSqlMCP.Helpers;
using ModelContextProtocol.Server;
using Microsoft.Data.SqlClient;
using MsSqlMCP;
using System.Text;

[McpServerToolType]
public static class SchemaTool
{
    // Helper method to sanitize database name to prevent SQL injection.
    // For SQL Server, names can be enclosed in []. This also escapes any existing ] characters.
    private static string SanitizeDatabaseName(string databaseName)
    {
        return $"[{databaseName.Replace("]", "]]")}]";
    }

    [McpServerTool, Description("Get tables name of database. Optionally, specify a database name to query a different database in the same instance.")]
    public async static Task<string> GetTables(string? databaseName = null)
    {
        var (connectionString, warning) = Program.GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            using var useDbCommand = new SqlCommand($"USE {SanitizeDatabaseName(databaseName)};", connection);
            await useDbCommand.ExecuteNonQueryAsync();
        }

        SchemaHelper schemaHelper = new SchemaHelper();
        var tables = await schemaHelper.GetTablesAsync(connection);
        string dbInfo = string.IsNullOrWhiteSpace(databaseName) ? string.Empty : $" in database '{databaseName}'";
        return $"Tables{dbInfo}:\n\n{string.Join("\n", tables)}";
    }

    [McpServerTool, Description("Get the columns (fields) of a database table. Optionally, specify a database name to query a different database in the same instance.")]
    public async static Task<string> GetColumns(string tableName, string? databaseName = null)
    {
        var (connectionString, warning) = Program.GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            using var useDbCommand = new SqlCommand($"USE {SanitizeDatabaseName(databaseName)};", connection);
            await useDbCommand.ExecuteNonQueryAsync();
        }

        SchemaHelper schemaHelper = new SchemaHelper();
        tableName = schemaHelper.ExtractTableName(tableName); 
        if (!string.IsNullOrEmpty(tableName))
        {
            var columns = await schemaHelper.GetColumnsAsync(connection, tableName);
            string dbInfo = string.IsNullOrWhiteSpace(databaseName) ? string.Empty : $" (database '{databaseName}')";
            return $"Columns in the table {tableName}{dbInfo}:\n\n{string.Join("\n", columns)}";
        }
        else
        {
            return "Please specify the table name to query its fields.";
        }
    }

    [McpServerTool, Description("Get the relationships between tables in the database. Optionally, specify a database name to query a different database in the same instance.")]
    public async static Task<string> GetRelationships(string? databaseName = null)
    {
        var (connectionString, warning) = Program.GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            using var useDbCommand = new SqlCommand($"USE {SanitizeDatabaseName(databaseName)};", connection);
            await useDbCommand.ExecuteNonQueryAsync();
        }

        SchemaHelper schemaHelper = new SchemaHelper();
        var relationships = await schemaHelper.GetRelationshipsAsync(connection);
        string dbInfo = string.IsNullOrWhiteSpace(databaseName) ? string.Empty : $" in database '{databaseName}'";
        return $"Relationships between tables{dbInfo}:\n\n{string.Join("\n", relationships)}";
    }

    [McpServerTool, Description("Execute a SQL query against the database. Does not allow DROP statements. Optionally, specify a database name to query a different database in the same instance.")]
    public async static Task<string> ExecuteSql(string sqlQuery, string? databaseName = null)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            return "SQL query cannot be empty.";
        }

        if (sqlQuery.Trim().ToUpperInvariant().StartsWith("DROP "))
        {
            return "Error: DROP statements are not allowed.";
        }

        var (connectionString, warningFlag) = Program.GetConnectionString(); 
        if (warningFlag)
        {
            // Potentially log the warning or handle it as needed
        }

        StringBuilder resultBuilder = new StringBuilder();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                using var useDbCommand = new SqlCommand($"USE {SanitizeDatabaseName(databaseName)};", connection);
                await useDbCommand.ExecuteNonQueryAsync();
                resultBuilder.AppendLine($"Switched to database '{SanitizeDatabaseName(databaseName)}'."); // Show sanitized name for clarity
            }

            using var command = new SqlCommand(sqlQuery, connection);
            bool isSelectQuery = sqlQuery.Trim().ToUpperInvariant().StartsWith("SELECT");

            if (isSelectQuery)
            {
                using var reader = await command.ExecuteReaderAsync();
                if (reader.HasRows)
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        resultBuilder.Append(reader.GetName(i));
                        if (i < reader.FieldCount - 1) resultBuilder.Append("\t|\t");
                    }
                    resultBuilder.AppendLine();
                    // Ensure separator line is not drawn if resultBuilder is empty or too short
                    if (resultBuilder.Length > 2) 
                    {
                        resultBuilder.AppendLine(new string('-', resultBuilder.Length - (resultBuilder.ToString().EndsWith(Environment.NewLine) ? Environment.NewLine.Length*2 : Environment.NewLine.Length) )); 
                    } else if (resultBuilder.Length > 0 && !resultBuilder.ToString().EndsWith(Environment.NewLine)) {
                        resultBuilder.AppendLine(new string('-', resultBuilder.Length));
                    }

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
            else
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

    [McpServerTool, Description("Get the definition of a stored procedure by name. Optionally, specify a database name to query a different database in the same instance.")]
    public async static Task<string> GetStoreProcedure(string spName, string? databaseName = null)
    {
        if (string.IsNullOrWhiteSpace(spName))
        {
            return "Stored procedure name cannot be empty.";
        }

        var (connectionString, warning) = Program.GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            using var useDbCommand = new SqlCommand($"USE {SanitizeDatabaseName(databaseName)};", connection);
            await useDbCommand.ExecuteNonQueryAsync();
        }

        SchemaHelper schemaHelper = new SchemaHelper();
        var procedures = await schemaHelper.GetStoreProcedureAsync(connection, spName);
        string dbInfo = string.IsNullOrWhiteSpace(databaseName) ? string.Empty : $" in database '{databaseName}'";
        if (procedures.Count == 0)
        {
            return $"Stored procedure '{spName}' not found{dbInfo}.";
        }
        return $"Stored procedure '{spName}'{dbInfo}:\n\n{string.Join("\n\n", procedures)}";
    }
}
