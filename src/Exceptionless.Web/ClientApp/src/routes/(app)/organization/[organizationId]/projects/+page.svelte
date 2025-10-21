<script lang="ts">
    import type { ViewProject } from '$features/projects/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import { organization } from '$features/organizations/context.svelte';
    import { type GetOrganizationProjectsParams, getOrganizationProjectsQuery } from '$features/projects/api.svelte';
    import { getTableOptions } from '$features/projects/components/table/options.svelte';
    import ProjectsDataTable from '$features/projects/components/table/projects-data-table.svelte';
    import { DEFAULT_LIMIT } from '$shared/api/api.svelte';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';

    const DEFAULT_PARAMS = {
        filter: '',
        limit: DEFAULT_LIMIT
    };

    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            filter: 'string',
            limit: 'number'
        }
    });

    const projectsQueryParameters: GetOrganizationProjectsParams = $state({
        get filter() {
            return queryParams.filter!;
        },
        set filter(value) {
            queryParams.filter = value;
        },
        get limit() {
            return queryParams.limit!;
        },
        set limit(value) {
            queryParams.limit = value;
        },
        mode: 'stats'
    });

    const projectsQuery = getOrganizationProjectsQuery({
        get params() {
            return projectsQueryParameters;
        },
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    const table = createTable(getTableOptions<ViewProject>(projectsQueryParameters, projectsQuery));

    async function rowClick(project: ViewProject) {
        if (project.id) {
            await goto(resolve('/(app)/project/[projectId]/manage', { projectId: project.id }));
        }
    }

    function rowHref(project: ViewProject): string {
        return resolve('/(app)/project/[projectId]/manage', { projectId: project.id });
    }

    async function addProject() {
        await goto(resolve('/(app)/project/add'));
    }

    $effect(() => {
        // Handle case where pop state loses the limit
        queryParams.limit ??= DEFAULT_LIMIT;
    });
</script>

<div class="flex flex-col gap-4">
    <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
            <H3>Projects</H3>
            <Muted>View and manage your projects. Click on a project to view its details.</Muted>
        </div>
        <div class="flex items-center gap-2">
            <Button size="icon" onclick={addProject} title="Add Project">
                <Plus class="size-4" aria-hidden="true" />
                <span class="sr-only">Add Project</span>
            </Button>
        </div>
    </div>
    <ProjectsDataTable bind:limit={projectsQueryParameters.limit!} isLoading={projectsQuery.isLoading} {rowClick} {rowHref} {table}>
        {#snippet toolbarChildren()}
            <div class="min-w-fit flex-1">
                <Input type="search" placeholder="Filter projects..." class="w-full" bind:value={projectsQueryParameters.filter} />
            </div>
        {/snippet}
    </ProjectsDataTable>
</div>
