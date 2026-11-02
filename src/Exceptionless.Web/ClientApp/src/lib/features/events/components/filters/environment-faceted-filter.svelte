<script lang="ts">
    import { getContext } from 'svelte';

    import type { FacetedFilterProps } from '#comp/faceted-filter/index.ts';

    import { type FilterScope, filterScopeKey } from '#comp/faceted-filter/filter-scope.ts';
    import * as FacetedFilter from '#comp/faceted-filter/index.ts';
    import { getOrganizationCountQuery } from '#features/events/api.svelte.ts';
    import { organization } from '#features/organizations/context.svelte.ts';
    import { getOrganizationProjectsQuery } from '#features/projects/api.svelte.ts';
    import { getEnvironmentKey, getListedEnvironmentNames, hasControlCharacters } from '#features/projects/environments.ts';
    import { terms } from '#features/shared/api/aggregations.ts';

    import { getProjectScopeFilter } from './helpers.svelte';
    import { EnvironmentFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, open = $bindable(false), title = 'Environment' }: FacetedFilterProps<EnvironmentFilter> = $props();
    const scope = getContext<FilterScope | undefined>(filterScopeKey);
    const countQuery = getOrganizationCountQuery({
        enabled: () => open,
        params: {
            aggregations: 'terms:(environment~100)',
            get filter() {
                return getProjectScopeFilter(scope?.filters ?? []);
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
    const projectsQuery = getOrganizationProjectsQuery({
        enabled: () => !!organization.current,
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    // Listed environments show in the casing the project chose. Matching stays case-insensitive.
    const listedNames = $derived(
        getListedEnvironmentNames(projectsQuery.data?.data ?? [], getProjectScopeFilter(scope?.filters ?? []).match(/[a-fA-F0-9]{24}/g) ?? undefined)
    );
    // A selected value wins over the aggregation key with the same name so it stays selected.
    const names = $derived.by(() => {
        const candidates = [...filter.value, ...(terms(countQuery.data?.aggregations, 'terms_environment')?.buckets?.map((bucket) => bucket.key) ?? [])];
        return candidates
            .filter((name, index) => !!name && candidates.findIndex((other) => getEnvironmentKey(other) === getEnvironmentKey(name)) === index)
            .sort();
    });
    const options = $derived([
        {
            label: 'Unspecified',
            value: ''
        },
        ...names.map((name) => ({
            label: listedNames.get(getEnvironmentKey(name)) ?? name,
            value: name
        }))
    ]);

    function createOption(search: string) {
        const value = search.trim().toLowerCase();
        return value && value.length <= 64 && !hasControlCharacters(value)
            ? {
                  label: listedNames.get(value) ?? value,
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
