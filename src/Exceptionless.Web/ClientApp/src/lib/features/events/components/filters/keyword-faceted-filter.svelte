<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';

    import { KeywordFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Raw Filter', ...props }: FacetedFilterProps<KeywordFilter> = $props();

    function toggleHidden() {
        filter.hidden = !filter.hidden;
        filterChanged(filter);
    }
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
    hidden={filter.hidden}
    {title}
    {toggleHidden}
    value={filter.value}
    {...props}
></FacetedFilter.Keyword>
