<script lang="ts">
    import { stackStatuses } from '$comp/events/options';
    import { StatusFilter, type IFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: StatusFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, title = 'Status', filterChanged, filterRemoved, ...props }: Props = $props();
</script>

<MultiselectFacetedFilter
    {title}
    bind:values={filter.value}
    options={stackStatuses}
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></MultiselectFacetedFilter>
