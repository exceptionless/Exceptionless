<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { getOrganizationCountQuery } from '$features/events/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { terms } from '$features/shared/api/aggregations';

    import { TagFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, open = $bindable(false), title = 'Tag', ...props }: FacetedFilterProps<TagFilter> = $props();

    // Store the organizationId to prevent loading when switching organizations.
    const organizationId = organization.current;

    // Create query with conditional enabled - only fetch when dropdown is open
    const countQuery = getOrganizationCountQuery({
        enabled: () => open,
        params: {
            aggregations: 'terms:tags'
        },
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const tags = $derived(Array.from(new Set(['Critical', ...(terms(countQuery.data?.aggregations, 'terms_tags')?.buckets?.map((tag) => tag.key) ?? [])])));
    const options = $derived(
        tags.map((tag) => ({
            label: tag,
            value: tag
        })) ?? []
    );

    $effect(() => {
        if (!countQuery.isSuccess || filter.value.length === 0) {
            return;
        }

        const selectedTags = tags.filter((tag) => filter.value.includes(tag));
        if (filter.value.length !== selectedTags.length) {
            filter.value = selectedTags.map((tag) => tag);
            filterChanged(filter);
        }
    });
</script>

<FacetedFilter.MultiSelect
    bind:open
    changed={(values: string[]) => {
        filter.value = values;
        filterChanged(filter);
    }}
    loading={countQuery.isLoading}
    {options}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {title}
    values={filter.value}
    {...props}
></FacetedFilter.MultiSelect>
