<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import { TypeFilter } from './models.svelte';
    import TypeFacetedFilter from './type-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Type' }: Props = $props();

    // Use getters to avoid state_referenced_locally warning - props are evaluated lazily
    const builder: FacetFilterBuilder<TypeFilter> = {
        component: TypeFacetedFilter,
        create: (filter?: TypeFilter) => filter ?? new TypeFilter(),
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    builderContext.set('type', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
