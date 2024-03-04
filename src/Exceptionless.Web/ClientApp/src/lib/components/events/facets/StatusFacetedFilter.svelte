<script lang="ts">
    import { stackStatuses } from '$comp/events/options';
    import { FacetedFilterDropdown } from '$comp/facets';
    import { StatusFilter } from '$comp/filters/filters';
    import type { StackStatus } from '$lib/models/api';
    import { createEventDispatcher } from 'svelte';

    const dispatch = createEventDispatcher();
    const filter = new StatusFilter([]);

    function onChanged({ detail }: CustomEvent<StackStatus[]>) {
        filter.values = detail;
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<FacetedFilterDropdown title="Status" options={stackStatuses} on:changed={onChanged} on:remove={onRemove}></FacetedFilterDropdown>
