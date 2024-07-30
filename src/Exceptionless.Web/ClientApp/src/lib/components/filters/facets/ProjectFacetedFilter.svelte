<script lang="ts">
    import { getProjectsByOrganizationIdQuery } from '$api/projectsApi.svelte';
    import { ProjectFilter } from '$comp/filters/filters.svelte';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { title = 'Status', filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<ProjectFilter> = $props();

    const response = getProjectsByOrganizationIdQuery({
        get organizationId() {
            return filter.organization;
        }
    });
    const options = $derived(
        response.data?.map((project) => ({
            value: project.id!,
            label: project.name!
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
    {title}
    values={filter.value}
    {options}
    loading={response.isLoading}
    noOptionsText="No projects found."
    changed={(values) => {
        filter.value = values;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    {...props}
></MultiselectFacetedFilter>
