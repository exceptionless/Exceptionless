<script lang="ts">
    import { getProjectsByOrganizationIdQuery } from '$api/projectsApi.svelte';
    import { ProjectFilter } from '$comp/filters/filters.svelte';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { title = 'Status', filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<ProjectFilter> = $props();

    // UPGRADE: Can this be $derived?
    let organizationId = $state<string | undefined>(filter.organization);

    const response = getProjectsByOrganizationIdQuery({
        get organizationId() {
            return organizationId;
        }
    });
    const options = $derived(
        response.data?.map((project) => ({
            value: project.id!,
            label: project.name!
        })) ?? []
    );

    $effect(() => {
        organizationId = filter.organization;

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
    bind:values={filter.value}
    {options}
    loading={response.isLoading}
    noOptionsText="No projects found."
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></MultiselectFacetedFilter>
