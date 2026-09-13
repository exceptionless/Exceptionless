<script lang="ts">
    import * as Tooltip from '$comp/ui/tooltip';

    import { TagFilter } from './models.svelte';
    import TagFacetedFilter from './tag-faceted-filter.svelte';
</script>

<Tooltip.Provider>
    <TagFacetedFilter filter={new TagFilter()} filterChanged={() => {}} filterRemoved={() => {}} open title="Tag" />
</Tooltip.Provider>
