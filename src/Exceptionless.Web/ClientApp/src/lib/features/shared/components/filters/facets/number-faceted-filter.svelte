<script lang="ts">
    import { NumberFilter } from '$comp/filters/filters.svelte';

    import type { FacetedFilterProps } from '.';

    import NumberFacetedFilter from './base/number-faceted-filter.svelte';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<NumberFilter> = $props();
</script>

<NumberFacetedFilter
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
></NumberFacetedFilter>
