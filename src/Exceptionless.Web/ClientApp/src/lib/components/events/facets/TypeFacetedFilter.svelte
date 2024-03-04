<script lang="ts">
    import { eventTypes } from '$comp/events/options';
    import { FacetedFilterDropdown } from '$comp/facets';
    import { TypeFilter } from '$comp/filters/filters';
    import type { PersistentEventKnownTypes } from '$lib/models/api';
    import { createEventDispatcher } from 'svelte';

    const dispatch = createEventDispatcher();
    const filter = new TypeFilter([]);

    function onChanged({ detail }: CustomEvent<PersistentEventKnownTypes[]>) {
        filter.values = detail;
        dispatch('changed', filter);
    }

    function onRemove() {
        dispatch('remove', filter);
    }
</script>

<FacetedFilterDropdown title="Type" options={eventTypes} on:changed={onChanged} on:remove={onRemove}></FacetedFilterDropdown>
