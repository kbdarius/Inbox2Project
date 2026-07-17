using Inbox2Project.Models;

namespace Inbox2Project.Services;

public sealed class ProjectDiscoveryService : IProjectDiscoveryService
{
    public IReadOnlyList<string> DiscoverProjects(string projectsRoot)
    {
        if (!Directory.Exists(projectsRoot))
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.CfgRootInvalid);
            throw new AppException(AppErrorId.CfgRootInvalid, userMessage, $"Projects root does not exist: {projectsRoot}");
        }

        var projects = Directory
            .GetDirectories(projectsRoot)
            .Where(path => Directory.Exists(Path.Combine(path, "EMAILS")))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (projects.Count == 0)
        {
            var (userMessage, _) = ErrorCatalog.Lookup(AppErrorId.PrjNoneFound);
            throw new AppException(AppErrorId.PrjNoneFound, userMessage, $"No project folders with EMAILS were found under: {projectsRoot}");
        }

        return projects;
    }
}
