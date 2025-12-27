<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import { SessionFilter } from './models.svelte';
    import SessionFacetedFilter from './session-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Session' }: Props = $props();

    // Use getters to avoid state_referenced_locally warning - props are evaluated lazily
    const builder: FacetFilterBuilder<SessionFilter> = {
        component: SessionFacetedFilter,
        create: (filter?: SessionFilter) => filter ?? new SessionFilter(),
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    builderContext.set('session', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
