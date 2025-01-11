<script lang="ts">
    import { ReferenceFilter } from '$comp/filters/filters.svelte';

    import type { FacetedFilterProps } from '.';

    import StringFacetedFilter from './base/string-faceted-filter.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Reference', ...props }: FacetedFilterProps<ReferenceFilter> = $props();
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
