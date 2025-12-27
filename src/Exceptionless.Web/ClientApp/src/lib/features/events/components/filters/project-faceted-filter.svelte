<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { organization } from '$features/organizations/context.svelte';
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';

    import { ProjectFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, open = $bindable(false), title = 'Project', ...props }: FacetedFilterProps<ProjectFilter> = $props();

    // Create query with conditional enabled - only fetch when dropdown is open and organization is available.
    // The organizationId getter ensures reactive updates when the organization changes.
    const projectsQuery = getOrganizationProjectsQuery({
        enabled: () => open && !!organization.current,
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    const projects = $derived(projectsQuery.data?.data ?? []);
    const options = $derived(
        projects.map((project) => ({
            label: project.name!,
            value: project.id!
        })) ?? []
    );

    $effect(() => {
        if (!projectsQuery.isSuccess || filter.value.length === 0) {
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
    bind:open
    changed={(values) => {
        filter.value = values;
        filterChanged(filter);
    }}
    loading={projectsQuery.isLoading}
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
