<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';

    import { DateFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Date Range', ...props }: FacetedFilterProps<DateFilter> = $props();

    const options = [
        { label: 'Last Hour', value: 'last hour' },
        { label: 'Last 24 Hours', value: 'last 24 hours' },
        { label: 'Last Week', value: 'last week' },
        { label: 'Last 30 Days', value: 'last 30 days' },
        { label: 'All Time', value: '' }
    ];

    if (isCustomDate(filter)) {
        options.push({ label: filter.value as string, value: filter.value as string });
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

<FacetedFilter.Dropdown
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    {options}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    {title}
    value={filter.value as string}
    {...props}
></FacetedFilter.Dropdown>
