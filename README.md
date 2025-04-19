# MsSqlMCP
MCP to query SQL Server database schema, such as tables, columns, and relationships

## Prerequisites:
- .NET 9
- SQL

## Configuration
To configure Copilot in Visual Studio Code, add the MCP server configuration to the `settings.json` file:

```json
"mcp": {
    "inputs": [],
    "servers": {
        "MsSqlMCP": {
            "type": "stdio",
            "command": "dotnet",
            "args": [
                "run",
                "--project",
                "c:\\{path of repository}\\MsSqlMCP\\MsSqlMCP.csproj"
            ]
        }
    }
}
