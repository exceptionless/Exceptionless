<script lang="ts">
    import { StringFilter } from '$comp/filters/filters.svelte';

    import type { FacetedFilterProps } from '.';

    import StringFacetedFilter from './base/string-faceted-filter.svelte';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<StringFilter> = $props();
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
    value={filter.value}
    {...props}
></StringFacetedFilter>
