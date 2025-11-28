# MsSqlMCP

MCP Server for SQL Server database schema inspection and read-only query execution.

## Features

- **Read-only access**: All queries are validated to prevent data modification (INSERT, UPDATE, DELETE, DROP, EXEC, etc. are blocked)
- **Schema discovery**: Tables, columns, relationships, and stored procedures
- **SQL execution**: Safe SELECT queries only
- **Dual transport**: Supports both stdio and HTTP/SSE protocols
- **Windows Service**: Can run as a Windows Service for production deployments
- **MCP Protocol**: Compatible with VS Code Copilot, Claude Desktop, and other MCP clients

## Prerequisites

- .NET 10 (or .NET 9 with minor adjustments)
- SQL Server

## Architecture

The project follows SOLID principles with dependency injection:

```
MsSqlMCP/
├── Program.cs                    # Entry point with DI and dual transport
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
│   └── ReadOnlySqlQueryValidator.cs # Security validation (27 blocked keywords)
└── Tests/
    └── ReadOnlySqlQueryValidatorTests.cs # 42 security tests
```

## Configuration

### Connection String

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=(local);Initial Catalog=YourDatabase;Encrypt=False;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Urls": "http://localhost:5000"
}
```

## Running

### Console Mode (Development)

```bash
# Run with both stdio and HTTP transports
dotnet run

# Run with HTTP transport only (for debugging)
dotnet run -- --http-only
```

### Run Tests

```bash
dotnet test --filter "FullyQualifiedName~Tests"
```

## MCP Client Configuration

### Option 1: stdio Transport (VS Code)

Add to your VS Code `settings.json`:

```json
{
  "mcp": {
    "servers": {
      "MsSqlMCP": {
        "type": "stdio",
        "command": "dotnet",
        "args": ["run", "--project", "c:\\path\\to\\MsSqlMCP.csproj"]
      }
    }
  }
}
```

### Option 2: HTTP Transport (VS Code)

First, start the server:

```bash
dotnet run -- --http-only
```

Then add to your VS Code `settings.json`:

```json
{
  "mcp": {
    "servers": {
      "MsSqlMCP": {
        "type": "http",
        "url": "http://localhost:5000/sse",
        "autoApprove": [
          "get_tables",
          "get_columns", 
          "get_relationships",
          "execute_sql",
          "get_store_procedure"
        ]
      }
    }
  }
}
```

### Option 3: Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "MsSqlMCP": {
      "command": "dotnet",
      "args": ["run", "--project", "c:\\path\\to\\MsSqlMCP.csproj"]
    }
  }
}
```

## Available Tools

| Tool | Description | Required Parameters |
|------|-------------|---------------------|
| `GetTables` | Get all table names in the database | None |
| `GetColumns` | Get columns (fields) for a specific table | `tableName` |
| `GetRelationships` | Get foreign key relationships between tables | None |
| `GetStoreProcedure` | Get stored procedure definition | `spName` |
| `ExecuteSql` | Execute a read-only SELECT query | `sqlQuery` |

All tools accept an optional `databaseName` parameter to query different databases in the same SQL Server instance.

### Security

The `ExecuteSql` tool only allows SELECT queries. The following statements are blocked:

- **DML**: INSERT, UPDATE, DELETE, MERGE, TRUNCATE
- **DDL**: CREATE, ALTER, DROP
- **DCL**: GRANT, REVOKE, DENY
- **Execution**: EXEC, EXECUTE, SP_EXECUTESQL, XP_
- **Others**: BACKUP, RESTORE, BULK, OPENROWSET, OPENQUERY, OPENDATASOURCE

## Windows Service Installation

### 1. Publish the Application

On your development machine:

```bash
cd c:\path\to\MsSqlMCP
dotnet publish -c Release -r win-x64 --self-contained true
```

This creates files in: `bin\Release\net10.0\win-x64\publish\`

### 2. Copy to Server

Copy the contents of the `publish` folder to the server:

```
Source: bin\Release\net10.0\win-x64\publish\*
Destination: C:\Services\MsSqlMCP\
```

### 3. Configure on Server

Edit `C:\Services\MsSqlMCP\appsettings.json` with your SQL Server connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=YOUR_SQL_SERVER;Initial Catalog=YOUR_DATABASE;Encrypt=False;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Urls": "http://localhost:5000"
}
```

### 4. Install the Service

Open **PowerShell as Administrator** and run:

```powershell
# Create the Windows Service
sc.exe create MsSqlMCP binPath= "C:\Services\MsSqlMCP\MsSqlMCP.exe --http-only" start= auto DisplayName= "MsSql MCP Server"

# Add description
sc.exe description MsSqlMCP "Model Context Protocol server for SQL Server database inspection"

# Create logs directory
mkdir C:\Services\MsSqlMCP\logs -Force

# Start the service
net start MsSqlMCP

# Verify status
sc.exe query MsSqlMCP
```

### 5. Verify Installation

```powershell
# Check service status
Get-Service -Name MsSqlMCP

# Test the endpoint
Invoke-RestMethod -Uri "http://localhost:5000/sse/tools"
```

### Service Management Commands

```powershell
# Stop service
net stop MsSqlMCP

# Start service
net start MsSqlMCP

# Restart service
net stop MsSqlMCP; net start MsSqlMCP

# Uninstall service
net stop MsSqlMCP
sc.exe delete MsSqlMCP
```

### Firewall Configuration (if accessing remotely)

```powershell
# Allow inbound traffic on port 5000
New-NetFirewallRule -DisplayName "MsSqlMCP" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow
```

## HTTP API Endpoints

When running in HTTP mode, the following endpoints are available:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/sse` | GET | SSE stream for MCP protocol |
| `/sse/tools` | GET | List all available tools |
| `/sse/invoke` | POST | Invoke a tool |

### Example: Invoke Tool via HTTP

```bash
curl -X POST http://localhost:5000/sse/invoke \
  -H "Content-Type: application/json" \
  -d '{"Tool": "GetTables", "Params": {}}'
```

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/sse/invoke" -Method POST -ContentType "application/json" -Body '{"Tool": "GetTables", "Params": {}}'
```

## Troubleshooting

### Service won't start

1. Check logs in `C:\Services\MsSqlMCP\logs\`
2. Verify connection string in `appsettings.json`
3. Ensure SQL Server is accessible from the service account
4. Run manually to see errors: `C:\Services\MsSqlMCP\MsSqlMCP.exe --http-only`

### Connection issues

1. Verify SQL Server is running
2. Check firewall rules for SQL Server port (1433)
3. If using Windows Authentication, ensure the service account has database access

### Port already in use

Change the port in `appsettings.json`:

```json
{
  "Urls": "http://localhost:5001"
}
```

## License

MIT
