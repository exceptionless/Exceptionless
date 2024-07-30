<script lang="ts">
    import { SessionFilter } from '$comp/filters/filters.svelte';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, title = 'Session', filterChanged, filterRemoved, ...props }: FacetedFilterProps<SessionFilter> = $props();
</script>

<StringFacetedFilter
    {title}
    value={filter.value}
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    {...props}
></StringFacetedFilter>
