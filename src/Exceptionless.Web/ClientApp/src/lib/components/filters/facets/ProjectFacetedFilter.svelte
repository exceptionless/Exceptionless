<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { derived, writable, type Writable } from 'svelte/store';

    import { getProjectsByOrganizationIdQuery } from '$api/projectsApi';
    import { ProjectFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: ProjectFilter;
    export let title: string = 'Status';
    export let open: Writable<boolean>;

    const organizationId = writable<string | null>(filter.organization ?? null);
    $: organizationId.set(filter.organization ?? null);

    const response = getProjectsByOrganizationIdQuery(organizationId);
    const options = derived(response, ($response) => {
        return (
            $response.data?.map((project) => ({
                value: project.id!,
                label: project.name!
            })) ?? []
        );
    });

    response.subscribe(($response) => {
        if (!$response.isSuccess || filter.value.length === 0) {
            return;
        }

        const projects = $response.data.filter((project) => filter.value.includes(project.id!));
        if (filter.value.length !== projects.length) {
            filter.value = projects.map((project) => project.id!);
            dispatch('changed', filter);
        }
    });

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<MultiselectFacetedFilter
    {open}
    {title}
    bind:values={filter.value}
    options={$options}
    loading={$response.isLoading}
    noOptionsText="No projects found."
    on:changed={onChanged}
    on:remove={onRemove}
></MultiselectFacetedFilter>
