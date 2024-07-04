<script lang="ts">
    import { ReferenceFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { title = 'Reference', filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<ReferenceFilter> = $props();
</script>

<StringFacetedFilter {title} bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}
></StringFacetedFilter>
