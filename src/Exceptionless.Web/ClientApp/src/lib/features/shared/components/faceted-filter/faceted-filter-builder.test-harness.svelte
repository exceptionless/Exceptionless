<script lang="ts">
    import * as Tooltip from '$comp/ui/tooltip';
    import DateFacetedFilterBuilder from '$features/events/components/filters/date-faceted-filter-builder.svelte';
    import KeywordFacetedFilterBuilder from '$features/events/components/filters/keyword-faceted-filter-builder.svelte';

    import type { IFilter } from './models';

    import FacetedFilterBuilder from './faceted-filter-builder.svelte';

    let { changed, filters, remove }: { changed: (filter: IFilter) => void; filters: IFilter[]; remove: (filter?: IFilter) => void } = $props();
</script>

<Tooltip.Provider>
    <FacetedFilterBuilder {filters} {changed} {remove}>
        <DateFacetedFilterBuilder term="date" title="Date" />
        <KeywordFacetedFilterBuilder />
    </FacetedFilterBuilder>
</Tooltip.Provider>
