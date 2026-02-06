using System.CommandLine;
using OpenBotLib;

Option<string> systemPromptOption = new("--system-prompt", "-s")
{
    Description = "The system prompt to use for the AI model."
};

RootCommand rootCommand = new("OpenBot CLI");
rootCommand.Options.Add(systemPromptOption);

ParseResult parseResult = rootCommand.Parse(args);
var systemPrompt = parseResult.GetValue(systemPromptOption) ?? "You are a helpful assistant.";

string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

ToolRunner toolRunner = new(toolName =>
{
    ConsoleColor previousColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Invoking tool: {toolName}");
    Console.ForegroundColor = previousColor;
    Console.ReadLine();
});

ChatClient chatClient = ChatClient.CreateOpenAI(apiKey, systemPrompt, tools: toolRunner.GetTools());

bool memorySaved = false;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    SaveMemory();
};

while (true)
{
    Console.Write("> ");
    string? userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput))
    {
        break;
    }

    string response = await chatClient.PromptAsync(userInput);
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine();
    Console.WriteLine($"{response}");
    Console.WriteLine();
    Console.ResetColor();
}

SaveMemory();

void SaveMemory()
{
    if (memorySaved) return;
    memorySaved = true;
    Console.WriteLine("Exiting... Saving memory.");
    chatClient.CompactAndSaveMemoryAsync().Wait();
}