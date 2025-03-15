<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { organization } from '$features/organizations/context.svelte';
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';

    import { ProjectFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Project', ...props }: FacetedFilterProps<ProjectFilter> = $props();

    // Store the organizationId to prevent loading when switching organizations.
    const organizationId = organization.current;
    const response = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const projects = $derived(response.data?.data ?? []);
    const options = $derived(
        projects.map((project) => ({
            label: project.name!,
            value: project.id!
        })) ?? []
    );

    $effect(() => {
        if (!response.isSuccess || filter.value.length === 0) {
            return;
        }

        const filteredProjects = projects.filter((project) => filter.value.includes(project.id!)) ?? [];
        if (filter.value.length !== filteredProjects.length) {
            filter.value = filteredProjects.map((project) => project.id!);
            filterChanged(filter);
        }
    });
</script>

<FacetedFilter.MultiSelect
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
></FacetedFilter.MultiSelect>
