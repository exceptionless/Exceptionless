<script lang="ts">
    import * as FacetedFilter from '$features/events/components/filters/index';
    import { organization } from '$features/organizations/context.svelte';
    import { type CustomFieldDefinition, getCustomFieldsQuery } from '$features/organizations/custom-fields';

    const organizationId = $derived(organization.current ?? '');

    const customFieldsQuery = getCustomFieldsQuery({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const SYSTEM_FIELD_NAMES = new Set(['haserror', 'sessionend']);
    const activeFields = $derived((customFieldsQuery.data ?? []).filter((f) => !f.isDeleted && !SYSTEM_FIELD_NAMES.has(f.name.toLowerCase())));

    function getFilterType(field: CustomFieldDefinition) {
        switch (field.indexType) {
            case 'bool':
                return 'boolean';
            case 'date':
                return 'date';
            case 'double':
            case 'float':
            case 'int':
            case 'long':
                return 'number';
            case 'keyword':
            case 'string':
            default:
                return 'string';
        }
    }
</script>

{#each activeFields as field (field.id)}
    {@const filterType = getFilterType(field)}
    {@const term = `idx.${field.name}`}
    {@const title = field.description || field.name}

    {#if filterType === 'boolean'}
        <FacetedFilter.BooleanBuilder {term} {title} />
    {:else if filterType === 'number'}
        <FacetedFilter.NumberBuilder {term} {title} />
    {:else if filterType === 'date'}
        <FacetedFilter.DateBuilder {term} {title} />
    {:else}
        <FacetedFilter.StringBuilder {term} {title} />
    {/if}
{/each}
