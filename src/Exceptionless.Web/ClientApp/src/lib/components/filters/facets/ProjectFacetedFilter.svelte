<script lang="ts">
    import { getProjectsByOrganizationIdQuery } from '$api/projectsApi';
    import { MultiselectFacetedFilter } from '$comp/faceted-filter';
    import { ProjectFilter } from '$comp/filters/filters';
    import { createEventDispatcher } from 'svelte';
    import { derived, writable } from 'svelte/store';

    const dispatch = createEventDispatcher();
    export let filter: ProjectFilter;
    export let title: string = 'Status';

    const organizationId = writable<string>(filter.organization);
    $: organizationId.set(filter.organization);

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

<MultiselectFacetedFilter {title} bind:values={filter.value} options={$options} loading={$response.isLoading} on:changed={onChanged} on:remove={onRemove}
></MultiselectFacetedFilter>
