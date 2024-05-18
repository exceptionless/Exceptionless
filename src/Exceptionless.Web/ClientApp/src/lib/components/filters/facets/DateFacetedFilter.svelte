<script lang="ts">
    import { DateFilter, type IFilter } from '$comp/filters/filters';
    import DropDownFacetedFilter from './base/DropDownFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: DateFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, title = 'Date Range', filterChanged, filterRemoved, ...props }: Props = $props();

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
