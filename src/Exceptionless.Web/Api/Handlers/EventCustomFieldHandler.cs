using System.ComponentModel.DataAnnotations;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Services;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Lock;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Api.Handlers;

public sealed class EventCustomFieldHandler(
    EventCustomFieldService eventCustomFieldService,
    IOrganizationRepository organizationRepository,
    ICustomFieldDefinitionRepository customFieldDefinitionRepository,
    ISavedViewRepository savedViewRepository,
    ILockProvider lockProvider,
    PersistentEventQueryValidator eventQueryValidator,
    EventStackQueryValidator eventStackQueryValidator,
    AppOptions options)
{
    public async Task<Result<IReadOnlyCollection<CustomFieldDefinitionResponse>>> Handle(GetEventCustomFields message)
    {
        var organization = await GetOrganizationAsync(message.Id, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        var results = await customFieldDefinitionRepository.FindByTenantAsync(nameof(PersistentEvent), message.Id);
        var fields = new List<CustomFieldDefinitionResponse>();
        do
        {
            fields.AddRange(results.Documents
                .Where(field => !EventCustomFieldService.IsSystemField(field.Name))
                .Select(CustomFieldDefinitionResponse.FromDefinition));
        } while (await results.NextPageAsync());

        return fields
            .OrderBy(field => field.DisplayOrder)
            .ThenBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<Result<CustomFieldDefinitionResponse>> Handle(CreateEventCustomField message)
    {
        var organization = await GetOrganizationAsync(message.Id, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (!organization.HasPremiumFeatures)
            return Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.PlanLimit, "Custom fields require a paid plan. Please upgrade to add custom fields."));

        if (EventCustomFieldService.IsSystemField(message.Field.Name))
            return Result.BadRequest($"'{message.Field.Name}' is a reserved system field and cannot be created manually.");

        EventCustomFieldService.CreateFieldResult createResult;
        try
        {
            createResult = await eventCustomFieldService.CreateFieldAsync(
                message.Id,
                message.Field.Name,
                message.Field.IndexType.ToLowerInvariant(),
                options.CustomFieldOptions.MaxFieldsPerOrganization,
                options.CustomFieldOptions.MaxLifetimeFieldsPerOrganization,
                message.Field.Description,
                message.Field.DisplayOrder,
                message.Context.RequestAborted);
        }
        catch (TimeoutException ex)
        {
            return Result.Conflict(ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or ValidationException or InvalidOperationException or DocumentValidationException)
        {
            return Result.Invalid(ValidationError.Create("general", ex.Message));
        }

        if (createResult.Status == EventCustomFieldService.CreateFieldStatus.Duplicate)
            return Result.Conflict($"A custom field named '{message.Field.Name}' already exists for this organization.");

        if (createResult.Status == EventCustomFieldService.CreateFieldStatus.ActiveLimitReached)
        {
            return Result.Invalid(ValidationError.Create(
                ApiValidationErrorIdentifiers.CustomFieldActiveLimit,
                $"Maximum of {options.CustomFieldOptions.MaxFieldsPerOrganization} active custom fields per organization has been reached."));
        }

        if (createResult.Status == EventCustomFieldService.CreateFieldStatus.LifetimeLimitReached)
        {
            return Result.Invalid(ValidationError.Create(
                ApiValidationErrorIdentifiers.CustomFieldLifetimeLimit,
                $"Maximum lifetime allocation of {options.CustomFieldOptions.MaxLifetimeFieldsPerOrganization} custom field slots per organization has been reached."));
        }

        var response = CustomFieldDefinitionResponse.FromDefinition(createResult.Definition!);
        return Result<CustomFieldDefinitionResponse>.Created(response, $"/api/v2/organizations/{message.Id}/event-custom-fields");
    }

    public async Task<Result<CustomFieldDefinitionResponse>> Handle(UpdateEventCustomField message)
    {
        var organization = await GetOrganizationAsync(message.Id, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (!organization.HasPremiumFeatures)
            return Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.PlanLimit, "Custom fields require a paid plan. Please upgrade to manage custom fields."));

        await using var consistencyLock = await TryAcquireConsistencyLockAsync(message.Id);
        if (consistencyLock is null)
            return Result.Conflict("Custom field or saved-view changes are already in progress for this organization. Please try again.");

        var definition = await GetDefinitionAsync(message.Id, message.FieldId);
        if (definition is null)
            return Result.NotFound("Custom field not found.");

        if (EventCustomFieldService.IsSystemField(definition.Name))
            return Result.BadRequest($"'{definition.Name}' is a reserved system field and cannot be modified.");

        var changes = message.Changes.GetEntity();
        if (message.Changes.ContainsChangedProperty(field => field.Description!) && changes.Description?.Length > UpdateCustomFieldDefinition.MaxDescriptionLength)
            return Result.Invalid(ValidationError.Create("description", $"Description cannot exceed {UpdateCustomFieldDefinition.MaxDescriptionLength} characters."));

        bool changed = false;
        if (message.Changes.ContainsChangedProperty(field => field.Description!))
        {
            definition.Description = String.IsNullOrEmpty(changes.Description) ? null : changes.Description;
            changed = true;
        }

        if (message.Changes.ContainsChangedProperty(field => field.DisplayOrder!) && changes.DisplayOrder.HasValue)
        {
            definition.DisplayOrder = changes.DisplayOrder.Value;
            changed = true;
        }

        if (changed)
            await customFieldDefinitionRepository.SaveAsync(definition);

        return CustomFieldDefinitionResponse.FromDefinition(definition);
    }

    public async Task<Result> Handle(DeleteEventCustomField message)
    {
        var organization = await GetOrganizationAsync(message.Id, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (!organization.HasPremiumFeatures)
            return Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.PlanLimit, "Custom fields require a paid plan. Please upgrade to manage custom fields."));

        await using var consistencyLock = await TryAcquireConsistencyLockAsync(message.Id);
        if (consistencyLock is null)
            return Result.Conflict("Custom field or saved-view changes are already in progress for this organization. Please try again.");

        var definition = await GetDefinitionAsync(message.Id, message.FieldId);
        if (definition is null)
            return Result.NotFound("Custom field not found.");

        if (EventCustomFieldService.IsSystemField(definition.Name))
            return Result.BadRequest($"'{definition.Name}' is a reserved system field and cannot be deleted.");

        var savedViews = await savedViewRepository.GetByOrganizationIdAsync(message.Id, o => o.SearchAfterPaging().PageLimit(1000));
        do
        {
            foreach (var savedView in savedViews.Documents)
            {
                var queryValidator = String.Equals(savedView.ViewType, "stacks", StringComparison.OrdinalIgnoreCase)
                    ? (AppQueryValidator)eventStackQueryValidator
                    : eventQueryValidator;
                var validation = await queryValidator.ValidateQueryAsync(savedView.Filter);
                if (!validation.IsValid)
                    continue;

                bool isReferenced = validation.ReferencedFields.Any(field =>
                    String.Equals(field, $"data.{definition.Name}", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(field, $"idx.{definition.Name}", StringComparison.OrdinalIgnoreCase));
                if (isReferenced)
                    return Result.Conflict($"Custom field '{definition.Name}' is used in one or more saved filters and cannot be deleted. Remove it from all filters first.");
            }
        } while (await savedViews.NextPageAsync());

        definition.IsDeleted = true;
        await customFieldDefinitionRepository.SaveAsync(definition);

        return Result.NoContent();
    }

    private async Task<Organization?> GetOrganizationAsync(string organizationId, HttpContext httpContext)
    {
        if (String.IsNullOrEmpty(organizationId) || !httpContext.Request.CanAccessOrganization(organizationId))
            return null;

        return await organizationRepository.GetByIdAsync(organizationId, o => o.Cache(false));
    }

    private async Task<CustomFieldDefinition?> GetDefinitionAsync(string organizationId, string fieldId)
    {
        var definition = await customFieldDefinitionRepository.GetByIdAsync(fieldId);
        return definition is null
            || definition.IsDeleted
            || !String.Equals(definition.TenantKey, organizationId, StringComparison.Ordinal)
            || !String.Equals(definition.EntityType, nameof(PersistentEvent), StringComparison.Ordinal)
            ? null
            : definition;
    }

    private Task<ILock?> TryAcquireConsistencyLockAsync(string organizationId)
        => lockProvider.TryAcquireAsync(
            EventCustomFieldService.GetSavedViewConsistencyLockName(organizationId),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(5));

}
