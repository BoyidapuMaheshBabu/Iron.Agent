namespace Iron.Agent.Core;

using System.Text.Json.Serialization;

public class GenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public class GenerateResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("response")]
    public string Response { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class GenerateChunk
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("response")]
    public string Response { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class TagsResponse
{
    [JsonPropertyName("models")]
    public List<ModelInfo> Models { get; set; }
}

public class ModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("modified_at")]
    public string ModifiedAt { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("digest")]
    public string Digest { get; set; }
}
