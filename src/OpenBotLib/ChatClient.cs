namespace OpenBotLib;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenAI;

public sealed class ChatClient : IDisposable
{
    private readonly IChatClient client;
    private readonly List<ChatMessage> chatHistory = [];
    private readonly ChatOptions? chatOptions;

    public const string DefaultSystemPrompt = "You are a helpful AI assistant. Answer the user's questions in a friendly and informative manner.";
    /*public const string DefaultOllamaUrl = "http://localhost:11434";
    public const string DefaultOllamaModel = "gpt-oss:20b";*/
    public const string DefaultOpenAIModel = "gpt-5.2";

    public static ChatClient CreateOpenAI(string apiKey, string? systemPrompt = null, (Delegate func, string description)[]? tools = null, string? model = null)
    {
        var openAIClient = new OpenAIClient(apiKey).GetChatClient(model ?? DefaultOpenAIModel).AsIChatClient();
        return new ChatClient(openAIClient, systemPrompt, tools);
    }

    /*
    public static ChatClient CreateOllama(string? url=null, string? systemPrompt = null, (Delegate func, string description)[]? tools = null, string? model = null)
    {
        var ollamaClient = new OllamaApiClient(url ?? DefaultOllamaUrl, model ?? DefaultOllamaModel);
        return new ChatClient(ollamaClient, systemPrompt, tools);
    }
    */

    public ChatClient(IChatClient client, string? systemPrompt = null, (Delegate func, string description)[]? tools = null)
    {
        this.client = client;
        if (tools != null && tools.Length > 0)
        {
            chatOptions = new()
            {
                Tools = [.. tools.Select(t => AIFunctionFactory.Create(t.func, description: t.description))]
            };
        }
        chatHistory.Add(new ChatMessage(ChatRole.System, systemPrompt ?? DefaultSystemPrompt));
        if (chatOptions != null)
        {
            this.client = new FunctionInvokingChatClient(client);
        }
    }

    public async Task<string> PromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));
        string response = $"{await client.GetResponseAsync(chatHistory, chatOptions, cancellationToken)}";
        chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));

        return response;
    }

    public async Task<T> PromptAsync<T>(string prompt, CancellationToken cancellationToken = default)
    {
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));
        var response = await client.GetResponseAsync<T>(chatHistory, chatOptions, useJsonSchemaResponseFormat: true, cancellationToken);
        chatHistory.Add(new ChatMessage(ChatRole.Assistant, $"{response}"));

        return response.Result;
    }

    public async IAsyncEnumerable<string> StreamPromptAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));
        await foreach (var response in client.GetStreamingResponseAsync(chatHistory, chatOptions, cancellationToken))
        {
            yield return $"{response}";
        }
    }

    public string GetChatHistoryString()
    {
        return string.Join("",
            chatHistory
                .Where(m => m.Role != ChatRole.System)
                .Select(message => $"""
            ----
            {message.Role}:
            {message}
            """)
        );
    }

    public IEnumerable<ChatMessage> GetChatHistory() => chatHistory;

    public void Dispose() => client.Dispose();
}