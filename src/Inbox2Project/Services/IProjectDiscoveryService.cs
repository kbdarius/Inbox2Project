namespace Inbox2Project.Services;

public interface IProjectDiscoveryService
{
    IReadOnlyList<string> DiscoverProjects(string projectsRoot);
}
