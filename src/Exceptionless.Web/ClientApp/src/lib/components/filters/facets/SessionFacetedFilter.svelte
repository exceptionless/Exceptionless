<script lang="ts">
    import { SessionFilter } from '$comp/filters/filters';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, title = 'Session', filterChanged, filterRemoved, ...props }: FacetedFilterProps<SessionFilter> = $props();
</script>

<StringFacetedFilter {title} bind:value={filter.value} changed={() => filterChanged(filter)} remove={() => filterRemoved(filter)} {...props}
></StringFacetedFilter>
