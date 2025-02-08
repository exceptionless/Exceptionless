<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';
    import { onDestroy } from 'svelte';

    import { VersionFilter } from './models.svelte';
    import VersionFacetedFilter from './version-faceted-filter.svelte';

    interface Props {
        priority?: number;
        term: string;
        title?: string;
    }

    const { priority = 0, term, title = 'Version' }: Props = $props();

    const builder: FacetFilterBuilder<VersionFilter> = {
        component: VersionFacetedFilter,
        create: (filter?: VersionFilter) => filter ?? new VersionFilter(term),
        priority,
        title
    };

    builderContext.set(`version-${term}`, builder as unknown as FacetFilterBuilder<IFilter>);
    onDestroy(() => builderContext.delete('version'));
</script>
