<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import { TagFilter } from './models.svelte';
    import TagFacetedFilter from './tag-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 0, title = 'Tag' }: Props = $props();

    // Use getters to avoid state_referenced_locally warning - props are evaluated lazily
    const builder: FacetFilterBuilder<TagFilter> = {
        component: TagFacetedFilter,
        create: (filter?: TagFilter) => filter ?? new TagFilter(),
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    builderContext.set('tag', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
