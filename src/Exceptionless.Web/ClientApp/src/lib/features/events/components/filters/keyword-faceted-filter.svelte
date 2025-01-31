<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';

    import { KeywordFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Keyword', ...props }: FacetedFilterProps<KeywordFilter> = $props();
</script>

<FacetedFilter.Keyword
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
></FacetedFilter.Keyword>
