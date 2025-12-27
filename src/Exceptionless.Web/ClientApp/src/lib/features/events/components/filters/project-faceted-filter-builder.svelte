<script lang="ts">
    import { builderContext, type FacetFilterBuilder, type IFilter } from '$comp/faceted-filter';

    import { ProjectFilter } from './models.svelte';
    import ProjectFacetedFilter from './project-faceted-filter.svelte';

    interface Props {
        priority?: number;
        title?: string;
    }

    const { priority = 100, title = 'Project' }: Props = $props();

    // Use getters to avoid state_referenced_locally warning - props are evaluated lazily
    const builder: FacetFilterBuilder<ProjectFilter> = {
        component: ProjectFacetedFilter,
        create: (filter?: ProjectFilter) => filter ?? new ProjectFilter(),
        get priority() {
            return priority;
        },
        get title() {
            return title;
        }
    };

    builderContext.set('project', builder as unknown as FacetFilterBuilder<IFilter>);
</script>
