<script lang="ts">
    import { getProjectsByOrganizationIdQuery } from '$api/projectsApi.svelte';
    import { ProjectFilter, type IFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: ProjectFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, title = 'Status', filterChanged, filterRemoved, ...props }: Props = $props();

    let organizationId = $state<string | null>(filter.organization ?? null);
    $effect(() => {
        organizationId = filter.organization ?? null;
    });

    const response = getProjectsByOrganizationIdQuery(organizationId);
    const options = $derived(
        $response.data?.map((project) => ({
            value: project.id!,
            label: project.name!
        })) ?? []
    );

    response.subscribe(($response) => {
        if (!$response.isSuccess || filter.value.length === 0) {
            return;
        }

        const projects = $response.data.filter((project) => filter.value.includes(project.id!));
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
    loading={$response.isLoading}
    noOptionsText="No projects found."
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></MultiselectFacetedFilter>
