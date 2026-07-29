using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Riok.Mapperly.Abstractions;

namespace Exceptionless.Web.Mapping;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class SavedViewMapper
{
    [MapperIgnoreTarget(nameof(SavedView.Version))]
    [MapperIgnoreTarget(nameof(SavedView.CreatedByUserId))]
    [MapperIgnoreTarget(nameof(SavedView.UpdatedByUserId))]
    public partial SavedView MapToSavedView(NewSavedView source);

    public partial ViewSavedView MapToViewSavedView(SavedView source);

    public partial List<ViewSavedView> MapToViewSavedViews(IEnumerable<SavedView> source);
}
