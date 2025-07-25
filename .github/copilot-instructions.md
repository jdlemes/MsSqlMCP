# MsSqlMCP - AI Coding Agent Instructions

## Project Overview
MsSqlMCP is a Model Context Protocol (MCP) server that provides SQL Server database schema exploration tools for AI agents. It exposes database metadata through MCP tools that can be called by AI assistants.

## Architecture & Key Components

### Core Pattern: MCP Tool Registration
- Tools are defined as static methods in `SchemaTool.cs` decorated with `[McpServerTool]`
- Each tool method is automatically registered via `WithToolsFromAssembly()` in `Program.cs`
- Tools follow async pattern: `public async static Task<string> ToolName(params)`

### Database Connection Strategy
- Connection string loaded from `appsettings.json` via `Program.GetConnectionString()`
- Always use `using var connection = new SqlConnection(connectionString)` pattern
- Database switching handled via `USE {SanitizeDatabaseName(databaseName)}` commands
- **Critical**: All database names must be sanitized using `SanitizeDatabaseName()` to prevent SQL injection

### Helper Layer Pattern
- `SchemaHelper.cs` contains all SQL queries and data access logic
- Tools in `SchemaTool.cs` handle MCP concerns (connection, database switching, formatting)
- Separation: SchemaTool = MCP interface, SchemaHelper = data access

## Development Workflows

### Adding New MCP Tools
1. Add method to `SchemaTool.cs` with `[McpServerTool, Description("...")]`
2. Implement data access method in `SchemaHelper.cs`
3. Follow connection pattern: open connection, switch database if needed, call helper
4. Return formatted string (not JSON) - MCP handles serialization

### Running & Testing
```bash
dotnet run --project MsSqlMCP.csproj
```
- Configure in VS Code `settings.json` under `"mcp"` section
- Test via Copilot chat which calls the MCP tools

### Configuration
- Database connection in `appsettings.json` under `ConnectionStrings.DefaultConnection`
- Supports Windows Authentication (`Trusted_Connection=True`) and SQL auth
- `appsettings.json` copied to output directory via `.csproj` configuration

## Code Conventions

### SQL Injection Prevention
- **Always** use `SanitizeDatabaseName()` for dynamic database names
- Use parameterized queries (`@parameter`) for user input in SchemaHelper
- Example: `command.Parameters.AddWithValue("@tableName", tableName)`

### SQL Query Restrictions
- `ExecuteSql` only allows read-only operations (SELECT, SHOW, DESCRIBE, etc.)
- Blocked statements: DROP, INSERT, UPDATE, DELETE, TRUNCATE, ALTER, CREATE, EXEC, EXECUTE, MERGE, BULK, BACKUP, RESTORE, DBCC, GRANT, DENY, REVOKE, USE, SHUTDOWN, KILL, RECONFIGURE
- Multi-statement queries are validated to prevent restriction bypass
- This ensures the tool remains a safe schema exploration utility

### Error Handling Pattern
```csharp
try {
    // SQL operations
} catch (SqlException ex) {
    return $"SQL Error: {ex.Message}";
} catch (Exception ex) {
    return $"Error: {ex.Message}";
}
```

### Query Result Formatting
- Tables: `schema.tablename` format
- Columns: `name | datatype(length) | NULL/NOT NULL IDENTITY PRIMARY KEY`
- Relationships: `table.column -> referenced_table.referenced_column (FK: name)`

## Key Dependencies
- `ModelContextProtocol` (v0.1.0-preview.9): Core MCP framework
- `Microsoft.Data.SqlClient`: SQL Server connectivity
- `Microsoft.Extensions.Hosting`: Dependency injection and configuration

## Schema Query Patterns
- Use `INFORMATION_SCHEMA` views for portable metadata queries
- Use `sys.*` views for SQL Server-specific features (foreign keys, procedures)
- Always include schema prefix in table references
- Order results consistently for predictable output

## Security Considerations
- All data-modifying statements explicitly blocked in `ExecuteSql`: DROP, INSERT, UPDATE, DELETE, TRUNCATE, ALTER, CREATE, EXEC/EXECUTE, MERGE, BULK, BACKUP, RESTORE, DBCC, GRANT, DENY, REVOKE, USE, SHUTDOWN, KILL, RECONFIGURE
- Multi-statement queries are validated to prevent bypassing restrictions
- Database name sanitization prevents injection
- Connection string supports both Windows and SQL authentication
- Only read-only operations permitted - this is a schema exploration tool only
- No direct file system access - database operations only
