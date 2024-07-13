<script lang="ts">
    import { NumberFilter } from '$comp/filters/filters.svelte';
    import NumberFacetedFilter from './base/NumberFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<NumberFilter> = $props();
</script>

<NumberFacetedFilter bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}></NumberFacetedFilter>
