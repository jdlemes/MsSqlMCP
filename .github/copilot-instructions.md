# MsSqlMCP - Copilot Instructions

## Project Overview
This is a **Model Context Protocol (MCP) server** that exposes SQL Server database schema inspection tools to AI assistants. It uses the `ModelContextProtocol` SDK with stdio transport for MCP protocol communication.

**IMPORTANT: This is a READ-ONLY server.** The `ExecuteSql` tool only allows SELECT queries. All modifying statements (INSERT, UPDATE, DELETE, DROP, etc.) are blocked by the `ReadOnlySqlQueryValidator`.

### Architecture Pattern
- **Entry point**: `Program.cs` uses top-level statements with `Host.CreateApplicationBuilder`
- **Tool registration**: Tools are defined in `SchemaTool.cs` with `[McpServerTool]` attributes
- **Dependency Injection**: Services are registered directly in Program.cs
- **Interfaces**: Located in `Interfaces/` folder for SOLID compliance
- **Services**: Located in `Services/` folder with implementations
- **Transport**: Stdio transport using `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`

### Project Structure
```
MsSqlMCP/
├── Program.cs                    # Entry point with DI configuration (top-level statements)
├── SchemaTool.cs                 # MCP tool definitions
├── Interfaces/
│   ├── IConnectionFactory.cs     # SQL connection abstraction
│   ├── IQueryExecutor.cs         # Query execution abstraction
│   ├── ISchemaRepository.cs      # Schema queries abstraction
│   └── ISqlQueryValidator.cs     # Query validation abstraction
├── Services/
│   ├── SqlConnectionFactory.cs   # Connection management
│   ├── SafeQueryExecutor.cs      # Validated query execution
│   ├── SchemaRepository.cs       # Schema query implementation
│   └── ReadOnlySqlQueryValidator.cs # Security validation
└── Tests/
    └── ReadOnlySqlQueryValidatorTests.cs # Security tests (42 tests)
```

## Key Conventions

### Dependency Injection Pattern
All services are registered in `Program.cs`:
```csharp
builder.Services.AddSingleton<IConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<ISqlQueryValidator, ReadOnlySqlQueryValidator>();
builder.Services.AddScoped<ISchemaRepository, SchemaRepository>();
builder.Services.AddScoped<IQueryExecutor, SafeQueryExecutor>();
builder.Services.AddScoped<SchemaTool>();
```

### Tool Definition Pattern
Tools in `SchemaTool.cs` use constructor injection:
```csharp
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

    [McpServerTool, Description("Tool description for AI")]
    public async Task<string> ToolName(string? databaseName = null)
    {
        // Use injected services
    }
}
```

### Security: Read-Only Query Validation
The `ReadOnlySqlQueryValidator` enforces read-only access by:
1. **Whitelist approach**: Only queries starting with `SELECT` or `WITH` are allowed
2. **Blocked keywords**: INSERT, UPDATE, DELETE, DROP, ALTER, CREATE, EXEC, TRUNCATE, MERGE, etc.
3. **Multiple statement detection**: Prevents SQL injection via semicolons

### Database Name Security
`SqlConnectionFactory.SanitizeDatabaseName()` prevents SQL injection:
- Wraps database names in `[]` brackets
- Escapes internal `]` characters as `]]`

### Optional Database Parameter
All tools accept `string? databaseName = null` to query different databases in the same SQL Server instance.

### Configuration Pattern
Connection string is loaded from `appsettings.json` via IConfiguration:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=(local);Initial Catalog=ia_oc;..."
  }
}
```

## Development Workflows

### Building and Running
```bash
# Run the MCP server (stdio mode)
dotnet run

# Run tests (filter required due to shared project)
dotnet test --filter "FullyQualifiedName~Tests"
```

### VS Code MCP Integration
Configure in `settings.json`:
```json
"mcp": {
  "servers": {
    "MsSqlMCP": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "c:\\path\\to\\MsSqlMCP.csproj"]
    }
  }
}
```

### Adding New Tools
1. Add async method to `SchemaTool` class
2. Decorate with `[McpServerTool, Description("...")]`
3. Use injected services (avoid creating dependencies with `new`)
4. Accept `string? databaseName = null` for consistency
5. Return `Task<string>` (MCP requirement)

### Adding New Services
1. Create interface in `Interfaces/` folder
2. Create implementation in `Services/` folder
3. Register in `Program.cs`
4. Inject via constructor in consuming classes

### Logging
All logs go to **stderr** via `LogToStandardErrorThreshold = LogLevel.Trace` to avoid polluting stdio transport.

## Dependencies
- **Microsoft.Data.SqlClient 6.0.1**: SQL Server connectivity
- **ModelContextProtocol 0.4.0-preview.1**: MCP SDK
- **ModelContextProtocol.AspNetCore 0.4.0-preview.1**: MCP ASP.NET Core integration
- **xUnit 2.9.2**: Unit testing framework
- **.NET 9.0**: Target framework
