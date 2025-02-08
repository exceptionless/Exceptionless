<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';
    import { onDestroy } from 'svelte';

    import { ProjectFilter } from './models.svelte';
    import ProjectFacetedFilter from './project-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 100, title = 'Project' }: Props = $props();

    const builder: FacetFilterBuilder<ProjectFilter> = {
        component: ProjectFacetedFilter,
        create: (filter?: ProjectFilter) => filter ?? new ProjectFilter(),
        priority,
        title
    };

    builderContext.set('project', builder as unknown as FacetFilterBuilder<IFilter>);
    onDestroy(() => builderContext.delete('project'));
</script>
