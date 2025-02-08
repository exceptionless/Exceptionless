<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';
    import { onDestroy } from 'svelte';

    import { StatusFilter } from './models.svelte';
    import StatusFacetedFilter from './status-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Status' }: Props = $props();

    const builder: FacetFilterBuilder<StatusFilter> = {
        component: StatusFacetedFilter,
        create: (filter?: StatusFilter) => filter ?? new StatusFilter(),
        priority,
        title
    };

    builderContext.set('status', builder as unknown as FacetFilterBuilder<IFilter>);
    onDestroy(() => builderContext.delete('status'));
</script>
