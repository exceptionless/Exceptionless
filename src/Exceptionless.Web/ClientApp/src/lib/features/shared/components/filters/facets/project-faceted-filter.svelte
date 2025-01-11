<script lang="ts">
    import { ProjectFilter } from '$comp/filters/filters.svelte';
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';

    import type { FacetedFilterProps } from '.';

    import MultiselectFacetedFilter from './base/multiselect-faceted-filter.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Status', ...props }: FacetedFilterProps<ProjectFilter> = $props();

    const response = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return filter.organization;
            }
        }
    });

    const options = $derived(
        response.data?.map((project) => ({
            label: project.name!,
            value: project.id!
        })) ?? []
    );

    $effect(() => {
        if (!response.isSuccess || filter.value.length === 0) {
            return;
        }

        const projects = response.data.filter((project) => filter.value.includes(project.id!));
        if (filter.value.length !== projects.length) {
            filter.value = projects.map((project) => project.id!);
            filterChanged(filter);
        }
    });
</script>

<MultiselectFacetedFilter
    changed={(values) => {
        filter.value = values;
        filterChanged(filter);
    }}
    loading={response.isLoading}
    noOptionsText="No projects found."
    {options}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {title}
    values={filter.value}
    {...props}
></MultiselectFacetedFilter>
