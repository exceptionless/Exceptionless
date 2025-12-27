<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import { StatusFilter } from './models.svelte';
    import StatusFacetedFilter from './status-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Status' }: Props = $props();

    // Use getters to avoid state_referenced_locally warning - props are evaluated lazily
    const builder: FacetFilterBuilder<StatusFilter> = {
        component: StatusFacetedFilter,
        create: (filter?: StatusFilter) => filter ?? new StatusFilter(),
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    builderContext.set('status', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
