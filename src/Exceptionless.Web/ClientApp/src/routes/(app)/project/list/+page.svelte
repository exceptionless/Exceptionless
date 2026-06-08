<script lang="ts">
    import type { ViewProject } from '$features/projects/models';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import DataTableViewOptions from '$comp/data-table/data-table-view-options.svelte';
    import { H3 } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import { organization } from '$features/organizations/context.svelte';
    import { useHideOrganizationNotifications } from '$features/organizations/hooks/use-hide-organization-notifications.svelte';
    import { type GetProjectsParams, getProjectsQuery } from '$features/projects/api.svelte';
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

    const projectsQueryParameters: GetProjectsParams = $state({
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

    const projectsQuery = getProjectsQuery({
        get params() {
            return projectsQueryParameters;
        }
    });

    const table = createTable(getTableOptions<ViewProject>(projectsQueryParameters, projectsQuery, { includeOrganizationColumn: true }));

    async function rowClick(project: ViewProject) {
        if (project.id) {
            organization.current = project.organization_id;
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
        queryParams.limit ??= DEFAULT_LIMIT;
    });

    useHideOrganizationNotifications();
</script>

<div class="flex flex-col gap-4">
    <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
            <H3>Projects</H3>
        </div>
    </div>
    <ProjectsDataTable bind:limit={projectsQueryParameters.limit!} isLoading={projectsQuery.isLoading} {rowClick} {rowHref} {table}>
        {#snippet toolbarChildren()}
            <Input type="search" placeholder="Filter projects or organizations..." class="flex-1" bind:value={projectsQueryParameters.filter} />
            <DataTableViewOptions size="icon-lg" {table} />
            <Button size="icon-lg" onclick={addProject} title="Add Project">
                <Plus class="size-4" aria-hidden="true" />
                <span class="sr-only">Add Project</span>
            </Button>
        {/snippet}
    </ProjectsDataTable>
</div>
