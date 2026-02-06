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

string? skillsDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".openbot", "skills");
if (!Directory.Exists(skillsDirectory))
{
    skillsDirectory = null;
}

ToolRunner toolRunner = new();

ChatClient chatClient = ChatClient.CreateOpenAI(apiKey, systemPrompt, tools: toolRunner.GetTools(), skillsDirectory: skillsDirectory);

while (true)
{
    Console.Write("> ");
    string? userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput))
    {
        break;
    }

    string response = await chatClient.PromptAsync(userInput);
    Console.WriteLine();
    Console.WriteLine($"{response}");
    Console.WriteLine();
}