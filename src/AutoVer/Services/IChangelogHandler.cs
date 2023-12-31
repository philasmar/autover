using AutoVer.Models;

namespace AutoVer.Services;

public interface IChangelogHandler
{
    string GenerateChangelogAsMarkdown(UserConfiguration configuration, string nextVersion);
    Task PersistChangelog(UserConfiguration configuration, string changelog, string? path);
}