<script lang="ts">
    import { stackStatuses } from '$comp/events/options';
    import { DropdownFacetedFilter } from '$comp/filters/facets';
    import { StatusFilter } from '$comp/filters/filters';
    import { createEventDispatcher } from 'svelte';

    const dispatch = createEventDispatcher();
    export let filter: StatusFilter;
    export let title: string = 'Status';

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<DropdownFacetedFilter {title} bind:values={filter.values} options={stackStatuses} on:changed={onChanged} on:remove={onRemove}></DropdownFacetedFilter>
