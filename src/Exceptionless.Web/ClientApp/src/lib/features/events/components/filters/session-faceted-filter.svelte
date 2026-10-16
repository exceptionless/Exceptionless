<script lang="ts">
    import type { FacetedFilterProps } from '#comp/faceted-filter/index.ts';

    import * as FacetedFilter from '#comp/faceted-filter/index.ts';

    import { SessionFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Session', ...props }: FacetedFilterProps<SessionFilter> = $props();

    function toggleHidden() {
        filter.hidden = !filter.hidden;
        filterChanged(filter);
    }
</script>

<FacetedFilter.String
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
></FacetedFilter.String>
