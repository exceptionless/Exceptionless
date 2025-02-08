<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';
    import { onDestroy } from 'svelte';

    import { SessionFilter } from './models.svelte';
    import SessionFacetedFilter from './session-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Session' }: Props = $props();

    const builder: FacetFilterBuilder<SessionFilter> = {
        component: SessionFacetedFilter,
        create: (filter?: SessionFilter) => filter ?? new SessionFilter(),
        priority,
        title
    };

    builderContext.set('session', builder as unknown as FacetFilterBuilder<IFilter>);
    onDestroy(() => builderContext.delete('session'));
</script>
