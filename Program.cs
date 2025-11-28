// MCP Server Entry Point - Supports both stdio and HTTP transports
// Can run as Console App or Windows Service
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using MsSqlMCP;
using MsSqlMCP.Interfaces;
using MsSqlMCP.Services;

// Detect if running in HTTP-only mode (for debugging)
bool httpOnly = args.Contains("--http-only");

// Configure content root to the application directory (important for Windows Service)
var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};

var builder = WebApplication.CreateBuilder(options);

// Enable Windows Service support (context-aware: works as console or service)
builder.Host.UseWindowsService();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Log to stderr to avoid polluting stdio transport
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Ensure logs directory exists for Windows Service mode
var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logsPath);

// Add timestamped console logging
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
});

// Register application services
builder.Services.AddSingleton<IConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<ISqlQueryValidator, ReadOnlySqlQueryValidator>();
builder.Services.AddScoped<ISchemaRepository, SchemaRepository>();
builder.Services.AddScoped<IQueryExecutor, SafeQueryExecutor>();
builder.Services.AddScoped<SchemaTool>();

// Configure MCP Server
var mcpBuilder = builder.Services.AddMcpServer();

if (!httpOnly)
{
    mcpBuilder.WithStdioServerTransport();
}

mcpBuilder.WithHttpTransport()
          .WithToolsFromAssembly();

var app = builder.Build();

// Map MCP endpoints (including /sse for SSE transport)
app.MapMcp();

// Endpoint to get the list of available tools
app.MapGet("/sse/tools", () =>
{
    var tools = DiscoverMcpTools();
    return Results.Ok(tools);
});

// Endpoint to invoke tools via HTTP
app.MapPost("/sse/invoke", async (HttpContext context, IServiceProvider serviceProvider) =>
{
    ToolInvokeRequest? request;
    try
    {
        request = await context.Request.ReadFromJsonAsync<ToolInvokeRequest>();
    }
    catch
    {
        return Results.BadRequest("Invalid JSON request");
    }
    
    if (request == null || string.IsNullOrEmpty(request.Tool))
    {
        return Results.BadRequest("Invalid request: Tool name is required");
    }

    try
    {
        // Find the tool method
        var toolTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

        foreach (var type in toolTypes)
        {
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == request.Tool &&
                                     m.GetCustomAttribute<McpServerToolAttribute>() != null);

            if (method != null)
            {
                // Create instance using DI
                using var scope = serviceProvider.CreateScope();
                var instance = scope.ServiceProvider.GetRequiredService(type);

                // Convert request parameters to method parameters
                var parameters = method.GetParameters();
                var methodArgs = new object?[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    if (request.Params != null && request.Params.TryGetValue(param.Name!, out var value))
                    {
                        if (value is JsonElement jsonElement)
                        {
                            methodArgs[i] = jsonElement.Deserialize(param.ParameterType);
                        }
                        else
                        {
                            methodArgs[i] = Convert.ChangeType(value, param.ParameterType);
                        }
                    }
                    else
                    {
                        methodArgs[i] = param.HasDefaultValue ? param.DefaultValue : null;
                    }
                }

                // Invoke the method
                var result = method.Invoke(instance, methodArgs);

                // If async, await the result
                if (result is Task task)
                {
                    await task;
                    var resultProperty = task.GetType().GetProperty("Result");
                    result = resultProperty?.GetValue(task);
                }

                return Results.Ok(result);
            }
        }

        return Results.NotFound($"Tool '{request.Tool}' not found");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error invoking tool: {ex.Message}");
    }
});

await app.RunAsync();

// Helper methods for tool discovery
static List<McpToolDescriptor> DiscoverMcpTools()
{
    var tools = new List<McpToolDescriptor>();

    var toolTypes = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => a.GetTypes())
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

    foreach (var type in toolTypes)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

        foreach (var method in methods)
        {
            var descriptor = new McpToolDescriptor
            {
                Name = method.Name,
                Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description,
                Parameters = BuildParametersSchema(method)
            };

            tools.Add(descriptor);
        }
    }

    return tools;
}

static JsonElement? BuildParametersSchema(MethodInfo method)
{
    var parameters = method.GetParameters();
    if (parameters.Length == 0)
    {
        return null;
    }

    var properties = new Dictionary<string, object>();
    var required = new List<string>();

    foreach (var param in parameters)
    {
        var paramType = Nullable.GetUnderlyingType(param.ParameterType) ?? param.ParameterType;
        var isNullable = param.ParameterType != paramType || param.HasDefaultValue;

        var paramSchema = new Dictionary<string, object>
        {
            ["type"] = GetJsonSchemaType(paramType)
        };

        var description = param.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (description != null)
        {
            paramSchema["description"] = description;
        }

        properties[param.Name!] = paramSchema;

        if (!isNullable && !param.HasDefaultValue)
        {
            required.Add(param.Name!);
        }
    }

    var schema = new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = properties
    };

    if (required.Count > 0)
    {
        schema["required"] = required;
    }

    var json = JsonSerializer.Serialize(schema);
    return JsonSerializer.Deserialize<JsonElement>(json);
}

static string GetJsonSchemaType(Type type)
{
    if (type == typeof(string)) return "string";
    if (type == typeof(int) || type == typeof(long)) return "integer";
    if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return "number";
    if (type == typeof(bool)) return "boolean";
    if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))) return "array";
    return "object";
}

// Types for HTTP endpoints
record ToolInvokeRequest(string Tool, Dictionary<string, object>? Params);

sealed class McpToolDescriptor
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public JsonElement? Parameters { get; set; }
}
