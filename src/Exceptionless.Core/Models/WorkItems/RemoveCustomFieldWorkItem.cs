namespace Exceptionless.Core.Models.WorkItems;

public record RemoveCustomFieldWorkItem
{
    public required string OrganizationId { get; init; }
    public required string CustomFieldDefinitionId { get; init; }
    public required string FieldName { get; init; }
}
