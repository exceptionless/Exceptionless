<script lang="ts">
    import { SessionFilter } from '$comp/filters/filters.svelte';

    import type { FacetedFilterProps } from '.';

    import StringFacetedFilter from './base/string-faceted-filter.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Session', ...props }: FacetedFilterProps<SessionFilter> = $props();
</script>

<StringFacetedFilter
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    {title}
    value={filter.value}
    {...props}
></StringFacetedFilter>
