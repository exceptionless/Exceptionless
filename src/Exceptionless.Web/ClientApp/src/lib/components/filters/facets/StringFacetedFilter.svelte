<script lang="ts">
    import { StringFilter } from '$comp/filters/filters.svelte';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<StringFilter> = $props();
</script>

<StringFacetedFilter
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
