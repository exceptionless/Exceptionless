<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';
    import { onMount } from 'svelte';

    import { ProjectFilter } from './models.svelte';
    import ProjectFacetedFilter from './project-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 100, title = 'Project' }: Props = $props();

    onMount(() => {
        const builder: FacetFilterBuilder<ProjectFilter> = {
            component: ProjectFacetedFilter,
            create: (filter?: ProjectFilter) => filter ?? new ProjectFilter(),
            priority,
            title
        };

        builderContext.set('project', builder as unknown as FacetFilterBuilder<IFilter>);
        return () => builderContext.delete('project');
    });
</script>
