using System.Text.Json.Serialization;

namespace MsSqlMCP.Models;

public class McpToolResult
{
    [JsonPropertyName("content")]
    public McpContent[] Content { get; set; } = [];

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}
