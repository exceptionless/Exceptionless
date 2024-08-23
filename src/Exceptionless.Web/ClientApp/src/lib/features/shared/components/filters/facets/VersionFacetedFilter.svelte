<script lang="ts">
    import { VersionFilter } from '$comp/filters/filters.svelte';

    import type { FacetedFilterProps } from '.';

    import StringFacetedFilter from './base/StringFacetedFilter.svelte';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<VersionFilter> = $props();
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
