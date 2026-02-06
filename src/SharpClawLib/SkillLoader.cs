namespace SharpClaw.SharpClawLib;

public static class SkillLoader
{
    public static SkillCatalog LoadFromDirectory(string skillsDirectory)
    {
        if (!Directory.Exists(skillsDirectory))
        {
            throw new DirectoryNotFoundException($"Skills directory not found: {skillsDirectory}");
        }

        var skills = new List<SkillDefinition>();
        foreach (var directory in Directory.EnumerateDirectories(skillsDirectory))
        {
            var skillFilePath = Path.Combine(directory, "SKILL.md");
            if (!File.Exists(skillFilePath))
            {
                continue;
            }

            var content = File.ReadAllText(skillFilePath);
            var skill = ParseSkillFile(skillFilePath, Path.GetFileName(directory), content);
            skills.Add(skill);
        }

        return new SkillCatalog(skills);
    }

    private static SkillDefinition ParseSkillFile(string skillFilePath, string directoryName, string content)
    {
        var lines = SplitLines(content);
        if (lines.Length == 0 || !IsFrontmatterDelimiter(lines[0]))
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' is missing YAML frontmatter.");
        }

        var endIndex = Array.FindIndex(lines, 1, IsFrontmatterDelimiter);
        if (endIndex < 0)
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' is missing the end of YAML frontmatter.");
        }

        var frontmatterLines = lines[1..endIndex];
        var body = string.Join(Environment.NewLine, lines[(endIndex + 1)..]);

        var metadata = ParseFrontmatter(frontmatterLines, skillFilePath);
        ValidateMetadata(metadata, directoryName, skillFilePath);

        return new SkillDefinition(metadata, body, content, Path.GetDirectoryName(skillFilePath)!);
    }

    private static SkillMetadata ParseFrontmatter(string[] frontmatterLines, string skillFilePath)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < frontmatterLines.Length; i++)
        {
            var line = frontmatterLines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.Equals("metadata:", StringComparison.OrdinalIgnoreCase))
            {
                var parentIndent = GetIndentation(line);
                i++;
                for (; i < frontmatterLines.Length; i++)
                {
                    var metadataLine = frontmatterLines[i];
                    if (string.IsNullOrWhiteSpace(metadataLine))
                    {
                        continue;
                    }

                    var metadataIndent = GetIndentation(metadataLine);
                    if (metadataIndent <= parentIndent)
                    {
                        i--;
                        break;
                    }

                    var metadataTrimmed = metadataLine.Trim();
                    if (metadataTrimmed.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var (key, value) = SplitKeyValue(metadataTrimmed, skillFilePath);
                    metadata[key] = value;
                }

                continue;
            }

            var (fieldKey, fieldValue) = SplitKeyValue(trimmed, skillFilePath);
            fields[fieldKey] = fieldValue;
        }

        fields.TryGetValue("name", out var name);
        fields.TryGetValue("description", out var description);
        fields.TryGetValue("license", out var license);
        fields.TryGetValue("compatibility", out var compatibility);
        fields.TryGetValue("allowed-tools", out var allowedTools);

        return new SkillMetadata(
            name ?? string.Empty,
            description ?? string.Empty,
            license,
            compatibility,
            metadata,
            allowedTools);
    }

    private static void ValidateMetadata(SkillMetadata metadata, string directoryName, string skillFilePath)
    {
        ValidateName(metadata.Name, directoryName, skillFilePath);
        ValidateDescription(metadata.Description, skillFilePath);

        if (!string.IsNullOrWhiteSpace(metadata.Compatibility) && metadata.Compatibility.Length > 500)
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' has compatibility longer than 500 characters.");
        }
    }

    private static void ValidateName(string name, string directoryName, string skillFilePath)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' is missing a name.");
        }

        if (name.Length is < 1 or > 64)
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' has name length outside 1-64 characters.");
        }

        if (!string.Equals(name, directoryName, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' name must match directory '{directoryName}'.");
        }

        if (name.StartsWith("-", StringComparison.Ordinal) || name.EndsWith("-", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' name must not start or end with '-'.");
        }

        if (name.Contains("--", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' name must not contain consecutive hyphens.");
        }

        foreach (var ch in name)
        {
            var isValid = ch == '-' || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
            if (!isValid)
            {
                throw new InvalidDataException($"Skill file '{skillFilePath}' name contains invalid characters.");
            }
        }
    }

    private static void ValidateDescription(string description, string skillFilePath)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' is missing a description.");
        }

        if (description.Length > 1024)
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' has description longer than 1024 characters.");
        }
    }

    private static (string Key, string Value) SplitKeyValue(string line, string skillFilePath)
    {
        var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            throw new InvalidDataException($"Skill file '{skillFilePath}' has invalid frontmatter line: {line}");
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();
        value = Unquote(value);

        return (key, value);
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            if ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
                (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal)))
            {
                return value[1..^1];
            }
        }

        return value;
    }

    private static int GetIndentation(string line)
    {
        var count = 0;
        while (count < line.Length && char.IsWhiteSpace(line[count]))
        {
            count++;
        }

        return count;
    }

    private static string[] SplitLines(string content) => content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static bool IsFrontmatterDelimiter(string line) => line.Trim().Equals("---", StringComparison.Ordinal);
}
