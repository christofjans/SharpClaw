namespace OpenBotLib;

using System.Diagnostics;

public class ToolRunner
{
    public (Delegate func, string description)[] GetTools() =>
    [
        (RunToolAsync, "Executes a PowerShell command and returns the output.")
    ];

    public async Task<string> RunToolAsync(string command)
    {
        string ps1FilePath = string.Empty;
        try
        {
            // get a temporary file path
            string tempFilePath = Path.GetTempFileName();

            // change extension to .ps1
            ps1FilePath = Path.ChangeExtension(tempFilePath, ".ps1");

            // write the command to the temporary file
            await File.WriteAllTextAsync(ps1FilePath, command);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = $"-noprofile -nologo -file \"{ps1FilePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output;
        }
        finally
        {
            // Clean up the temporary file
            if (File.Exists(ps1FilePath))
                File.Delete(ps1FilePath);
        }
    }
}