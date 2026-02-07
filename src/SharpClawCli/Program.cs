using System.CommandLine;
using SharpClaw.SharpClawLib;

Option<string> systemPromptOption = new("--system-prompt", "-s")
{
    Description = "The system prompt to use for the AI model."
};

Option<int> pulseOption = new("--pulse", "-p")
{
    Description = "The idle time before a pulse (in minutes).",
    DefaultValueFactory = (r) => -1
};

Option<bool> yoloOption = new("--yolo", "-y")
{
    Description = "Skip user confirmation for tool invocations."
};

RootCommand rootCommand = new("SharpClaw CLI");
rootCommand.Options.Add(systemPromptOption);
rootCommand.Options.Add(pulseOption);
rootCommand.Options.Add(yoloOption);

ParseResult parseResult = rootCommand.Parse(args);
var systemPrompt = parseResult.GetValue(systemPromptOption) ?? "You are a helpful assistant.";
int pulseMinutes = parseResult.GetValue(pulseOption);
bool yolo = parseResult.GetValue(yoloOption);

string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");

ToolRunner toolRunner = new(toolName =>
{
    ConsoleColor previousColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Invoking tool: {toolName}");
    Console.ForegroundColor = previousColor;
    if (!yolo)
    {
        Console.ReadLine();
    }
});

ChatClient chatClient = ChatClient.CreateOpenAI(apiKey, systemPrompt, tools: toolRunner.GetTools());
chatClient.RegisterSkillActivatedCallback(skillName =>
{
    ConsoleColor previousColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"Skill activated: {skillName}");
    Console.ForegroundColor = previousColor;
});

bool memorySaved = false;

CancellationTokenSource cts = new();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    SaveMemory();
};


DateTime lastInteraction = DateTime.Now;
while (true)
{
    Console.Write("> ");

    string? userInput;
    if (pulseMinutes>0)
    {
        var readTask = Task.Run(() => Console.ReadLine());
        var delayTask = Task.Delay(pulseMinutes * 60 * 1000, cts.Token);

        var completed = await Task.WhenAny(readTask, delayTask);

        if (completed == delayTask)
        {
            Console.WriteLine("Pulse triggered due to inactivity.");
            var pulseFilePath = Path.Combine(Directory.GetCurrentDirectory(), "PULSE.md");
            if (File.Exists(pulseFilePath))
            {
                var pulsePrompt = await File.ReadAllTextAsync(pulseFilePath);
                string pulseResponse = await chatClient.PulseAsync(pulsePrompt);
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine();
                Console.WriteLine($"{pulseResponse}");
                Console.WriteLine();
                Console.ResetColor();

            }
            continue;
        }

        userInput = await readTask;
    }
    else
    {
        userInput = Console.ReadLine();
    }

    lastInteraction = DateTime.Now;
    if (string.IsNullOrWhiteSpace(userInput))
    {
        continue;
    }
    if (userInput.Trim().ToLower() == "/exit")
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
    Console.WriteLine("Exiting... ");
    chatClient.CompactAndSaveMemoryAsync().Wait();
}