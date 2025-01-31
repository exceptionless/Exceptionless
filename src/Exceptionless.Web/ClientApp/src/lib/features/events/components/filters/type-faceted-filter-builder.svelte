<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';
    import { onMount } from 'svelte';

    import { TypeFilter } from './models.svelte';
    import TypeFacetedFilter from './type-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Type' }: Props = $props();

    onMount(() => {
        const builder: FacetFilterBuilder<TypeFilter> = {
            component: TypeFacetedFilter,
            create: (filter?: TypeFilter) => filter ?? new TypeFilter(),
            priority,
            title
        };

        builderContext.set('type', builder as unknown as FacetFilterBuilder<IFilter>);
        return () => builderContext.delete('type');
    });
</script>
