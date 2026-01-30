using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Riok.Mapperly.Abstractions;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Mapperly-based mapper for Project types.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class ProjectMapper
{
    public partial Project MapToProject(NewProject source);

    [MapperIgnoreTarget(nameof(ViewProject.HasSlackIntegration))]
    private partial ViewProject MapToViewProjectCore(Project source);

    public ViewProject MapToViewProject(Project source)
    {
        var result = MapToViewProjectCore(source);
        result.HasSlackIntegration = source.Data is not null && source.Data.ContainsKey(Project.KnownDataKeys.SlackToken);
        return result;
    }

    public List<ViewProject> MapToViewProjects(IEnumerable<Project> source)
        => source.Select(MapToViewProject).ToList();
}
