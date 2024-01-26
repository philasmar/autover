using System.Globalization;
using System.Text;
using AutoVer.Constants;
using AutoVer.Exceptions;
using AutoVer.Models;
using AutoVer.Services.IO;

namespace AutoVer.Services;

public class ChangelogHandler(
    IGitHandler gitHandler,
    IFileManager fileManager,
    IPathManager pathManager) : IChangelogHandler
{
    public string GenerateChangelogAsMarkdown(UserConfiguration configuration)
    {
        var tags = gitHandler.GetTags(configuration);
        var versionNumbers = tags
            .Where(x => x.StartsWith("version_"))
            .Select(x => x.Replace("version_", ""))
            .Select(x => DateTime.ParseExact(x, "yyyy-MM-dd.HH.mm.ss", CultureInfo.InvariantCulture))
            .OrderDescending()
            .ToList();
        if (versionNumbers.Count == 0)
            throw new InvalidVersionTag($"The Git repository '{configuration.GitRoot}' does not have a valid version tag. Please run 'autover version' first.");
        var currentVersionDate = versionNumbers[0];
        
        var changelog = new StringBuilder();
        changelog.AppendLine($"## Release {currentVersionDate:yyyy-MM-dd}");
        
        if (configuration.UseCommitsForChangelog)
        {
            var commits = new List<ConventionalCommit>();
            if (versionNumbers.Count > 1)
            {
                var lastVersionDate = versionNumbers[1];
                var lastVersionTag = $"version_{lastVersionDate:yyyy-MM-dd.HH.mm.ss}";
                
                commits = gitHandler.GetVersionCommits(configuration, lastVersionTag);
            }
            else
            {
                commits = gitHandler.GetVersionCommits(configuration);
            }

            changelog.AppendLine();
            var commitTypes = 
                commits
                    .Select(x => x.Type)
                    .Distinct()
                    .Order()
                    .ToList();
            foreach (var type in commitTypes)
            {
                var typeCommits = 
                    commits
                        .Where(x => x.Type.Equals(type))
                        .Distinct()
                        .OrderBy(x => x.Scope)
                        .ToList();
                if (configuration.ChangelogCategories != null &&
                    configuration.ChangelogCategories.TryGetValue(type, out var typeLabel))
                {
                }
                else if (ChangelogConstants.ChangelogCategories.TryGetValue(type, out typeLabel))
                {
                }
                else
                {
                    typeLabel = type;
                }
                changelog.AppendLine($"### {typeLabel}");
                foreach (var commit in typeCommits)
                {
                    if (string.IsNullOrEmpty(commit.Scope))
                    {
                        changelog.AppendLine($"* {commit.Description}");
                    }
                    else
                    {
                        changelog.AppendLine($"* **{commit.Scope}**: {commit.Description}");
                    }
                }
            }
            
        }
        else
        {
            foreach (var project in configuration.Projects)
            {
                if (project.ProjectDefinition is null)
                    throw new InvalidProjectException($"The project '{project.Path}' is invalid.");
                
                if (project.Changelog.Count == 0)
                    continue;
                
                var projectName = GetProjectName(project.ProjectDefinition.ProjectPath);
                changelog.AppendLine();
                changelog.AppendLine($"### {projectName}");

                changelog.AppendLine();
                foreach (var entry in project.Changelog)
                {
                    changelog.AppendLine($"* {entry}");
                }
            }
        }

        return changelog.ToString();
    }

    public async Task PersistChangelog(UserConfiguration configuration, string changelog, string? path)
    {
        if (string.IsNullOrEmpty(configuration.GitRoot))
            throw new InvalidProjectException("The project path you have specified is not a valid git repository.");
        
        if (string.IsNullOrEmpty(path))
        {
            path = pathManager.Combine(configuration.GitRoot, ChangelogConstants.DefaultChangelogFileName);
        }
        
        if (fileManager.Exists(path))
        {
            var existingChangelog = await fileManager.ReadAllTextAsync(path);
            await fileManager.WriteAllTextAsync(path, $"{changelog}\n{existingChangelog}");
        }
        else
        {
            await fileManager.WriteAllTextAsync(path, changelog);
        }
        
        gitHandler.StageChanges(configuration, path);
    }

    private string GetProjectName(string projectPath)
    {
        var projectParts = projectPath.Split(Path.DirectorySeparatorChar);
        if (projectParts.Length == 0)
            throw new InvalidProjectException($"The project '{projectPath}' is invalid.");
        var projectFileName = projectParts.Last();
        var projectFileNameParts = projectFileName.Split('.');
        if (projectFileNameParts.Length != 2)
            throw new InvalidProjectException($"The project '{projectPath}' is invalid.");
        return projectFileNameParts.First();
    }
}