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

    private partial ViewSavedView MapToViewSavedViewCore(SavedView source);

    public ViewSavedView MapToViewSavedView(SavedView source)
    {
        var result = MapToViewSavedViewCore(source);
        result.ColumnSettings ??= SavedViewColumnSettings.FromLegacy(source.Columns, source.ColumnOrder);
        return result;
    }

    public List<ViewSavedView> MapToViewSavedViews(IEnumerable<SavedView> source)
        => source.Select(MapToViewSavedView).ToList();
}
