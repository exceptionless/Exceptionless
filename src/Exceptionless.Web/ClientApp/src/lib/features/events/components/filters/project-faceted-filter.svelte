<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { organization } from '$features/organizations/context.svelte';
    // TODO: look at why we go across features here
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';

    import { ProjectFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Project', ...props }: FacetedFilterProps<ProjectFilter> = $props();

    const response = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return organization.current;
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
