<script lang="ts">
    import type { FacetedFilterProps } from '$comp/faceted-filter';

    import * as FacetedFilter from '$comp/faceted-filter';
    import { logLevels } from '$features/events/options';

    import { LevelFilter } from './models.svelte';

    let { filter, filterChanged, filterRemoved, title = 'Log Level', ...props }: FacetedFilterProps<LevelFilter> = $props();

    function toggleHidden() {
        filter.hidden = !filter.hidden;
        filterChanged(filter);
    }
</script>

<FacetedFilter.MultiSelect
    changed={(values: string[]) => {
        filter.value = values;
        filterChanged(filter);
    }}
    options={logLevels}
    remove={() => {
        filter.value = [];
        filterRemoved(filter);
    }}
    hidden={filter.hidden}
    {title}
    {toggleHidden}
    values={filter.value}
    {...props}
></FacetedFilter.MultiSelect>
