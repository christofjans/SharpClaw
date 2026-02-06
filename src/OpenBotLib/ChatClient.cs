namespace OpenBotLib;

using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using OpenAI;

public sealed class ChatClient : IDisposable
{
    private readonly IChatClient client;
    private readonly List<ChatMessage> chatHistory = [];
    private readonly ChatOptions? chatOptions;
    private SkillCatalog? skills;
    private string? memoryFilePath = null;
    private readonly HashSet<string> activatedSkills = new(StringComparer.OrdinalIgnoreCase);

    public const string DefaultSystemPrompt = "You are a helpful AI assistant. Answer the user's questions in a friendly and informative manner.";
    /*public const string DefaultOllamaUrl = "http://localhost:11434";
    public const string DefaultOllamaModel = "gpt-oss:20b";*/
    public const string DefaultOpenAIModel = "gpt-5.2";

    private sealed record SkillSelection([property: JsonPropertyName("skillName")] string? SkillName);

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
        var effectiveSystemPrompt = BuildSystemPrompt(systemPrompt ?? DefaultSystemPrompt);
        chatHistory.Add(new ChatMessage(ChatRole.System, effectiveSystemPrompt));
        if (chatOptions != null)
        {
            this.client = new FunctionInvokingChatClient(client);
        }
    }

    public async Task<string> PromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        await ActivateSkillIfNeededAsync(prompt, cancellationToken);
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));
        string response = $"{await client.GetResponseAsync(chatHistory, chatOptions, cancellationToken)}";
        chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));

        return response;
    }

    public async Task<T> PromptAsync<T>(string prompt, CancellationToken cancellationToken = default)
    {
        await ActivateSkillIfNeededAsync(prompt, cancellationToken);
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));
        var response = await client.GetResponseAsync<T>(chatHistory, chatOptions, useJsonSchemaResponseFormat: true, cancellationToken);
        chatHistory.Add(new ChatMessage(ChatRole.Assistant, $"{response}"));

        return response.Result;
    }

    public async IAsyncEnumerable<string> StreamPromptAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await ActivateSkillIfNeededAsync(prompt, cancellationToken);
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));
        await foreach (var response in client.GetStreamingResponseAsync(chatHistory, chatOptions, cancellationToken))
        {
            yield return $"{response}";
        }
    }

    public void ActivateSkill(string skillName)
    {
        if (skills is null || skills.IsEmpty)
        {
            throw new InvalidOperationException("Skill support is not configured.");
        }

        if (!skills.TryGetSkill(skillName, out var skill) || skill is null)
        {
            throw new KeyNotFoundException($"Skill '{skillName}' not found.");
        }

        if (!activatedSkills.Add(skill.Metadata.Name))
        {
            return;
        }

        chatHistory.Add(new ChatMessage(ChatRole.System, $"Activated skill: {skill.Metadata.Name}{Environment.NewLine}{skill.FullContent}"));
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

    public async Task CompactAndSaveMemoryAsync()
    {
        if (memoryFilePath is null)
        {
            return;
        }
        
        var transcript = GetChatHistoryString();
        List<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, MemoryCompactionPrompt),
            new ChatMessage(ChatRole.User, $"""
            <TRANSCRIPT>
            {transcript}
            </TRANSCRIPT>
            """),
        ];
        var selection = await client.GetResponseAsync<string[]>(messages);

        File.AppendAllLines(memoryFilePath, selection.Result);
    }

    public IEnumerable<ChatMessage> GetChatHistory() => chatHistory;

    public void Dispose() => client.Dispose();

    private async Task ActivateSkillIfNeededAsync(string prompt, CancellationToken cancellationToken)
    {
        if (skills is null || skills.IsEmpty)
        {
            return;
        }

        if (activatedSkills.Count >= skills.Skills.Count)
        {
            return;
        }

        var metadataSection = skills.BuildMetadataPromptSection();
        if (string.IsNullOrWhiteSpace(metadataSection))
        {
            return;
        }

        List<ChatMessage> selectionMessages =
        [
            new ChatMessage(ChatRole.System, BuildSkillSelectionSystemPrompt(metadataSection)),
            new ChatMessage(ChatRole.User, prompt)
        ];

        var selection = await client.GetResponseAsync<SkillSelection>(
            selectionMessages,
            null,
            useJsonSchemaResponseFormat: true,
            cancellationToken);

        var skillName = selection.Result.SkillName?.Trim();
        if (string.IsNullOrWhiteSpace(skillName) ||
            skillName.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            skillName.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!skills.TryGetSkill(skillName, out _))
        {
            throw new InvalidOperationException($"Skill selection returned unknown skill '{skillName}'.");
        }

        ActivateSkill(skillName);
    }

    private static SkillCatalog? LoadSkills()
    {
        var skillsDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".openbot", "skills");
        if (!Directory.Exists(skillsDirectory))
        {
            return null;
        }

        return SkillLoader.LoadFromDirectory(skillsDirectory);
    }

    private string BuildSystemPrompt(string basePrompt)
    {
        skills = LoadSkills();
        var prompt = basePrompt;

        var agentsFile = Path.Combine(Directory.GetCurrentDirectory(), "AGENTS.md");
        if (File.Exists(agentsFile))
        {
            var agentsContent = File.ReadAllText(agentsFile);
            if (!string.IsNullOrWhiteSpace(agentsContent))
            {
                prompt = $"{prompt}{Environment.NewLine}{Environment.NewLine}{agentsContent}";
            }
        }

        if (skills is not null && !skills.IsEmpty)
        {
            var metadataSection = skills.BuildMetadataPromptSection();
            if (!string.IsNullOrWhiteSpace(metadataSection))
            {
                prompt = $"{prompt}{Environment.NewLine}{Environment.NewLine}{metadataSection}";
            }
        }

        memoryFilePath = Path.Combine(Directory.GetCurrentDirectory(), "MEMORY.md");
        if (File.Exists(memoryFilePath))
        {
            var memoryContent = File.ReadAllText(memoryFilePath);
            if (!string.IsNullOrWhiteSpace(memoryContent))
            {
                prompt = $"{prompt}{Environment.NewLine}{Environment.NewLine}Here are you memories so far:{Environment.NewLine}{memoryContent}";
            }
        }
        else
        {
            memoryFilePath = null;
        }

        return prompt;
    }

    private static string BuildSkillSelectionSystemPrompt(string metadataSection) => $"""
        You are a skill selection assistant. Decide whether activating a skill would improve the response to the user's prompt.
        Choose at most one skill, and only when it would clearly help. Do not rely on keyword matching or simple heuristics.
        Respond using JSON with a single property "skillName" set to the exact skill name from the list, or null if no skill should be activated.

        {metadataSection}
        """;

    private static string MemoryCompactionPrompt = """
    You are a memory extraction engine for an AI companion.
    Your task is to extract durable, factual information from a chat transcript.

    Rules:
    - Extract only long-term, actionable, or identity-related facts.
    - Ignore emotional language, flirting, greetings, filler, and roleplay.
    - Prefer facts that affect future behavior, permissions, capabilities, preferences, or configuration.
    - Do NOT infer facts. Only extract what is explicitly stated.
    - Do NOT rewrite or paraphrase into speculative language.
    - Do NOT include transient states (mood, feelings, one-off reactions).
    - If no durable facts exist, output an empty list.

    Output format:
    - Return valid JSON only.
    - Use a JSON array of string.
    - Each string contains a single fact.
    """;
}
