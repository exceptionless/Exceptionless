<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import type { Writable } from 'svelte/store';

    import { DateFilter } from '$comp/filters/filters';
    import DropDownFacetedFilter from './base/DropDownFacetedFilter.svelte';

    const dispatch = createEventDispatcher();
    export let filter: DateFilter;
    export let title: string = 'Date Range';
    export let open: Writable<boolean>;

    const options = [
        { value: 'last hour', label: 'Last Hour' },
        { value: 'last 24 hours', label: 'Last 24 Hours' },
        { value: 'last week', label: 'Last Week' },
        { value: 'last 30 days', label: 'Last 30 Days' },
        { value: '', label: 'All Time' }
    ];

    if (isCustomDate(filter)) {
        options.push({ value: filter.value as string, label: filter.value as string });
    }

    let value = filter.value as string;
    function onChanged() {
        filter.value = value;
        dispatch('changed', filter);
    }

    function onRemove() {
        filter.value = value;
        dispatch('remove', filter);
    }

    function isCustomDate(filter: DateFilter) {
        if (filter.value === undefined) {
            return false;
        }

        if (filter.value === '' || (typeof filter.value === 'string' && filter.value.startsWith('last'))) {
            return false;
        }

        return true;
    }
</script>

<DropDownFacetedFilter {open} {title} bind:value {options} on:changed={onChanged} on:remove={onRemove}></DropDownFacetedFilter>
