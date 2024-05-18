<script lang="ts">
    import { NumberFilter, type IFilter } from '$comp/filters/filters';
    import NumberFacetedFilter from './base/NumberFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: NumberFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, filterChanged, filterRemoved, ...props }: Props = $props();
</script>

<NumberFacetedFilter bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}></NumberFacetedFilter>
