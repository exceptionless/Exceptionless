using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
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

        return fields;
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

        CustomFieldDefinition? definition;
        try
        {
            definition = await eventCustomFieldService.CreateFieldAsync(
                message.Id,
                message.Field.Name,
                message.Field.IndexType.ToLowerInvariant(),
                options.CustomFieldOptions.MaxFieldsPerOrganization,
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

        if (definition is null)
        {
            var existingPage = await customFieldDefinitionRepository.FindByTenantAsync(nameof(PersistentEvent), message.Id);
            var allActive = new List<CustomFieldDefinition>();
            do
            {
                allActive.AddRange(existingPage.Documents);
            } while (await existingPage.NextPageAsync());

            if (allActive.Any(field => String.Equals(field.Name, message.Field.Name, StringComparison.OrdinalIgnoreCase)))
                return Result.BadRequest($"A custom field named '{message.Field.Name}' already exists for this organization.");

            return Result.BadRequest($"Maximum of {options.CustomFieldOptions.MaxFieldsPerOrganization} custom fields per organization has been reached.");
        }

        var response = CustomFieldDefinitionResponse.FromDefinition(definition);
        return Result<CustomFieldDefinitionResponse>.Created(response, $"/api/v2/organizations/{message.Id}/event-custom-fields");
    }

    public async Task<Result<CustomFieldDefinitionResponse>> Handle(UpdateEventCustomField message)
    {
        var organization = await GetOrganizationAsync(message.Id, message.Context);
        if (organization is null)
            return Result.NotFound("Organization not found.");

        if (!organization.HasPremiumFeatures)
            return Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.PlanLimit, "Custom fields require a paid plan. Please upgrade to manage custom fields."));

        var definition = await GetDefinitionAsync(message.Id, message.FieldId);
        if (definition is null)
            return Result.NotFound("Custom field not found.");

        if (EventCustomFieldService.IsSystemField(definition.Name))
            return Result.BadRequest($"'{definition.Name}' is a reserved system field and cannot be modified.");

        bool changed = false;
        if (message.Field.Description is not null)
        {
            definition.Description = message.Field.Description.Length == 0 ? null : message.Field.Description;
            changed = true;
        }

        if (message.Field.DisplayOrder.HasValue)
        {
            definition.DisplayOrder = message.Field.DisplayOrder.Value;
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

        var definition = await GetDefinitionAsync(message.Id, message.FieldId);
        if (definition is null)
            return Result.NotFound("Custom field not found.");

        if (EventCustomFieldService.IsSystemField(definition.Name))
            return Result.BadRequest($"'{definition.Name}' is a reserved system field and cannot be deleted.");

        var fieldNamePattern = BuildFieldNameRegex(definition.Name);
        var savedViews = await savedViewRepository.GetByOrganizationIdAsync(message.Id, o => o.SearchAfterPaging().PageLimit(1000));
        do
        {
            if (savedViews.Documents.Any(view => IsCustomFieldUsedInFilter(view.Filter, fieldNamePattern)))
                return Result.Conflict($"Custom field '{definition.Name}' is used in one or more saved filters and cannot be deleted. Remove it from all filters first.");
        } while (await savedViews.NextPageAsync());

        definition.IsDeleted = true;
        await customFieldDefinitionRepository.SaveAsync(definition);

        return Result.Accepted();
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

    private static Regex BuildFieldNameRegex(string fieldName)
    {
        var escapedName = Regex.Escape(fieldName);
        return new Regex($@"(?<![a-zA-Z0-9_.-])(?:idx|data)\.{escapedName}(?![a-zA-Z0-9_.-])", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }

    private static bool IsCustomFieldUsedInFilter(string? filter, Regex fieldNamePattern)
        => !String.IsNullOrWhiteSpace(filter) && fieldNamePattern.IsMatch(filter);
}
