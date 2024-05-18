<script lang="ts">
    import { eventTypes } from '$comp/events/options';
    import { TypeFilter, type IFilter } from '$comp/filters/filters';
    import MultiselectFacetedFilter from './base/MultiselectFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: TypeFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { title = 'Type', filter, filterChanged, filterRemoved, ...props }: Props = $props();
</script>

<MultiselectFacetedFilter
    {title}
    bind:values={filter.value}
    options={eventTypes}
    changed={() => filterChanged(filter)}
    remove={() => filterRemoved(filter)}
    {...props}
></MultiselectFacetedFilter>
