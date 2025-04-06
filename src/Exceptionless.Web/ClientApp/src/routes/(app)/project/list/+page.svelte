<!-- Project List Page -->
<script lang="ts">
    import type { ViewProject } from '$features/projects/models';

    import { goto } from '$app/navigation';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Input } from '$comp/ui/input';
    import { organization } from '$features/organizations/context.svelte';
    import { getOrganizationProjectsQuery } from '$features/projects/api.svelte';
    import { getTableContext } from '$features/projects/components/table/options.svelte';
    import ProjectsDataTable from '$features/projects/components/table/projects-data-table.svelte';
    import { DEFAULT_LIMIT } from '$shared/api/api.svelte';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import Plus from 'lucide-svelte/icons/plus';
    import { watch } from 'runed';

    let filter = $state('');

    const DEFAULT_PARAMS = {
        limit: DEFAULT_LIMIT
    };

    const params = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number'
        }
    });

    const context = getTableContext<ViewProject>({ limit: params.limit!, mode: 'stats' });
    const table = createTable(context.options);

    async function rowClick(project: ViewProject) {
        if (project.id) {
            await goto(`/next/project/${project.id}/manage`);
        }
    }

    const projectsQuery = getOrganizationProjectsQuery({
        params: context.parameters,
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });

    watch(
        () => projectsQuery.dataUpdatedAt,
        () => {
            if (projectsQuery.isSuccess) {
                context.data = projectsQuery.data.data || [];
                context.meta = projectsQuery.data.meta;
            }
        }
    );

    $effect(() => {
        // Handle case where pop state loses the limit
        params.limit ??= DEFAULT_LIMIT;
    });
</script>

<div class="flex flex-col space-y-4">
    <Card.Root>
        <Card.Header>
            <Card.Title class="text-2xl" level={2}>My Projects</Card.Title>
            <Card.Description>
                <Button href="/next/project/add">
                    <Plus class="mr-2 size-4" />
                    Add New Project
                </Button>
            </Card.Description>
        </Card.Header>
        <Card.Content class="pt-4">
            <ProjectsDataTable bind:limit={params.limit!} isLoading={projectsQuery.isLoading} {rowClick} {table}>
                {#snippet toolbarChildren()}
                    <Input type="search" placeholder="Filter projects..." bind:value={filter} />
                {/snippet}
            </ProjectsDataTable>
        </Card.Content>
    </Card.Root>
</div>
