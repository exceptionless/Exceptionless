<script lang="ts">
    import { BooleanFilter, type IFilter } from '$comp/filters/filters';
    import BooleanFacetedFilter from './base/BooleanFacetedFilter.svelte';

    interface Props {
        title: string;
        open: boolean;
        filter: BooleanFilter;
        filterChanged: (filter: IFilter) => void;
        filterRemoved: (filter: IFilter) => void;
    }

    let { filter, filterChanged, filterRemoved, ...props }: Props = $props();
</script>

<BooleanFacetedFilter bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}></BooleanFacetedFilter>
