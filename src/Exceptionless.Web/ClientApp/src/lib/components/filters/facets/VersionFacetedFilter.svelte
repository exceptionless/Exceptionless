<script lang="ts">
    import { VersionFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<VersionFilter> = $props();
</script>

<StringFacetedFilter bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}></StringFacetedFilter>
