<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { type FilterScope, filterScopeKey } from '$comp/faceted-filter/filter-scope';
    import { getOrganizationCountQuery } from '$features/events/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { terms } from '$features/shared/api/aggregations';
    import { getContext } from 'svelte';

    import { toFilter } from './helpers.svelte';
    import { EnvironmentFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, open = $bindable(false), title = 'Environment' }: FacetedFilterProps<EnvironmentFilter> = $props();
    const scope = getContext<FilterScope | undefined>(filterScopeKey);
    const countQuery = getOrganizationCountQuery({
        enabled: () => open,
        params: {
            aggregations: 'terms:(environment~100)',
            get filter() {
                return toFilter(scope?.filters.filter((filter) => filter.type === 'project') ?? []);
            },
            get time() {
                return scope?.time ?? undefined;
            }
        },
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const names = $derived(
        [...new Set([...filter.value, ...(terms(countQuery.data?.aggregations, 'terms_environment')?.buckets?.map((bucket) => bucket.key) ?? [])])]
            .filter(Boolean)
            .sort()
    );
    const options = $derived([
        {
            label: 'Unspecified',
            value: ''
        },
        ...names.map((name) => ({
            label: name,
            value: name
        }))
    ]);

    function createOption(search: string) {
        const value = search.trim().toLowerCase();
        // eslint-disable-next-line no-control-regex -- Deployment names cannot contain control characters.
        return value && value.length <= 64 && !/[\u0000-\u001f\u007f-\u009f]/u.test(value)
            ? {
                  label: value,
                  value
              }
            : undefined;
    }
</script>

<FacetedFilter.MultiSelect
    bind:open
    changed={(values) => {
        filter.value = values;
        filterChanged(filter);
    }}
    {createOption}
    emptyText="All environments"
    hidden={filter.hidden}
    loading={countQuery.isLoading}
    {options}
    remove={() => filterRemoved(filter)}
    {title}
    toggleHidden={() => {
        filter.hidden = !filter.hidden;
        filterChanged(filter);
    }}
    values={filter.value}
/>
