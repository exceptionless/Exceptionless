<script lang="ts">
    import { DateFilter } from '$comp/filters/filters.svelte';
    import DropDownFacetedFilter from './base/DropDownFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, title = 'Date Range', filterChanged, filterRemoved, ...props }: FacetedFilterProps<DateFilter> = $props();

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

<DropDownFacetedFilter
    {title}
    bind:value={filter.value as string}
    {options}
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></DropDownFacetedFilter>
