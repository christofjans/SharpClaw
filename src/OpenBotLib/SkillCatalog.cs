namespace OpenBotLib;

using System.Text;

public sealed record SkillMetadata(
    string Name,
    string Description,
    string? License,
    string? Compatibility,
    IReadOnlyDictionary<string, string> Metadata,
    string? AllowedTools);

public sealed record SkillDefinition(
    SkillMetadata Metadata,
    string Body,
    string FullContent,
    string DirectoryPath);

public sealed class SkillCatalog
{
    private readonly Dictionary<string, SkillDefinition> skillsByName;

    public SkillCatalog(IEnumerable<SkillDefinition> skills)
    {
        var skillList = skills.ToList();
        Skills = skillList;
        skillsByName = skillList.ToDictionary(skill => skill.Metadata.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<SkillDefinition> Skills { get; }

    public bool IsEmpty => Skills.Count == 0;

    public bool TryGetSkill(string name, out SkillDefinition? skill) => skillsByName.TryGetValue(name, out skill);

    public string BuildMetadataPromptSection()
    {
        if (Skills.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Available skills:");
        foreach (var skill in Skills)
        {
            builder.AppendLine($"- {skill.Metadata.Name}: {skill.Metadata.Description}");
        }

        return builder.ToString().TrimEnd();
    }
}
