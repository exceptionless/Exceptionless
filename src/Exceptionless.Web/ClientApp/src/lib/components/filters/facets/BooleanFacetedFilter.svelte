<script lang="ts">
    import { BooleanFilter } from '$comp/filters/filters.svelte';
    import BooleanFacetedFilter from './base/BooleanFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<BooleanFilter> = $props();
</script>

<BooleanFacetedFilter bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}></BooleanFacetedFilter>
