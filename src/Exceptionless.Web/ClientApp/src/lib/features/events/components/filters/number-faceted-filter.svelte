<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';

    import { NumberFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, ...props }: FacetedFilterProps<NumberFilter> = $props();

    function toggleHidden() {
        filter.hidden = !filter.hidden;
        filterChanged(filter);
    }
</script>

<FacetedFilter.Number
    changed={(value) => {
        filter.value = value;
        filterChanged(filter);
    }}
    remove={() => {
        filter.value = undefined;
        filterRemoved(filter);
    }}
    hidden={filter.hidden}
    {toggleHidden}
    value={filter.value}
    {...props}
></FacetedFilter.Number>
