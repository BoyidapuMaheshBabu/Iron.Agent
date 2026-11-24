namespace Iron.Agent.Core;

using System.Net.Http.Json;

public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OllamaClient(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl), Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<string> GenerateAsync(string prompt, string model, CancellationToken cancellationToken = default)
    {
        var request = new GenerateRequest { Model = model, Prompt = prompt, Stream = false };
        var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GenerateResponse>(cancellationToken: cancellationToken);
        return result?.Response ?? string.Empty;
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(string prompt, string model, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new GenerateRequest { Model = model, Prompt = prompt, Stream = true };
        var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            GenerateChunk chunk = null;
            try
            {
                chunk = System.Text.Json.JsonSerializer.Deserialize<GenerateChunk>(line);
            }
            catch
            {
                // Skip malformed lines
            }

            if (chunk != null && !string.IsNullOrEmpty(chunk.Response))
            {
                yield return chunk.Response;
            }
            if (chunk?.Done == true) break;
        }
    }

    public async Task<string> ChatAsync(string systemPrompt, string userMessage, string model, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage { Role = "system", Content = systemPrompt },
            new ChatMessage { Role = "user", Content = userMessage }
        };
        var request = new ChatRequest { Model = model, Messages = messages, Stream = false };
        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);
        return result?.Message?.Content ?? string.Empty;
    }

    public async Task<bool> IsModelAvailableAsync(string model, CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await ListModelsAsync(cancellationToken);
            return models.Contains(model);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string[]> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken: cancellationToken);
        return result?.Models.Select(m => m.Name).ToArray() ?? Array.Empty<string>();
    }
}
