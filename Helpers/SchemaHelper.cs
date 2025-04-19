using Microsoft.Data.SqlClient;

namespace MsSqlMCP.Helpers
{
    public class SchemaHelper
    {
        
        
        public async Task<List<string>> GetTablesAsync(SqlConnection connection)
        {
            var tables = new List<string>();

            string sql = @"
        SELECT TABLE_SCHEMA, TABLE_NAME 
        FROM INFORMATION_SCHEMA.TABLES 
        WHERE TABLE_TYPE = 'BASE TABLE'
        ORDER BY TABLE_SCHEMA, TABLE_NAME";

            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string schema = reader.GetString(0);
                string tableName = reader.GetString(1);
                tables.Add($"{schema}.{tableName}");
            }

            return tables;
        }

        public async Task<List<string>> GetColumnsAsync(SqlConnection connection, string tableName)
        {
            var columns = new List<string>();

            string schema = "dbo"; // default value
            if (tableName.Contains("."))
            {
                var parts = tableName.Split('.');
                schema = parts[0];
                tableName = parts[1];
            }

            string sql = @"
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
            command.Parameters.AddWithValue("@tableName", tableName);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string columnName = reader.GetString(0);
                string dataType = reader.GetString(1);
                object charMaxLength = reader.GetValue(2);
                string isNullable = reader.GetString(3);
                int isIdentity = reader.GetInt32(4);
                int isPrimaryKey = reader.GetInt32(5);

                string lengthInfo = charMaxLength == DBNull.Value ? "" : $"({charMaxLength})";
                string nullableInfo = isNullable == "YES" ? "NULL" : "NOT NULL";
                string identityInfo = isIdentity == 1 ? "IDENTITY" : "";
                string pkInfo = isPrimaryKey == 1 ? "PRIMARY KEY" : "";

                columns.Add($"{columnName} | {dataType}{lengthInfo} | {nullableInfo} {identityInfo} {pkInfo}".Trim());
            }

            return columns;
        }

        public async Task<List<string>> GetRelationshipsAsync(SqlConnection connection)
        {
            var relationships = new List<string>();

            string sql = @"
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

            while (await reader.ReadAsync())
            {
                string foreignKey = reader.GetString(0);
                string tableName = reader.GetString(1);
                string columnName = reader.GetString(2);
                string referencedTableName = reader.GetString(3);
                string referencedColumnName = reader.GetString(4);

                relationships.Add($"{tableName}.{columnName} -> {referencedTableName}.{referencedColumnName} (FK: {foreignKey})");
            }

            return relationships;
        }

        public string ExtractTableName(string query)
        {
            
            var words = query.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if ((words[i].ToLower() == "tabla" || words[i].ToLower() == "table") && i + 1 < words.Length)
                {
                    return words[i + 1].Trim(',', '.', ':', ';', '?', '!');
                }
                else
                {
                    return words[i].Trim(',', '.', ':', ';', '?', '!');
                }
            }
            return string.Empty;
        }

    }
}
