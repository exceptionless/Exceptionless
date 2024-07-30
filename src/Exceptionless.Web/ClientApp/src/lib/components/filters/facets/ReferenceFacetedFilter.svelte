<script lang="ts">
    import { ReferenceFilter } from '$comp/filters/filters.svelte';
    import StringFacetedFilter from './base/StringFacetedFilter.svelte';
    import type { FacetedFilterProps } from '.';

    let { title = 'Reference', filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<ReferenceFilter> = $props();
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
