<script lang="ts">
    import { StringFilter } from '$comp/filters/filters.svelte';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<StringFilter> = $props();
</script>

<StringFacetedFilter bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}></StringFacetedFilter>
