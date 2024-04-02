<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import type { Writable } from 'svelte/store';

    import { stackStatuses } from '$comp/events/options';
    import { StatusFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: StatusFilter;
    export let title: string = 'Status';
    export let open: Writable<boolean>;

    function onChanged() {
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<MultiselectFacetedFilter {open} {title} bind:values={filter.value} options={stackStatuses} on:changed={onChanged} on:remove={onRemove}
></MultiselectFacetedFilter>
