<script lang="ts">
    import { page } from '$app/state';
    import { A, H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { getProjectConfig, postProjectConfig } from '$features/projects/api.svelte';
    import AddProjectConfigDialog from '$features/projects/components/dialogs/add-project-config-dialog.svelte';
    import { getTableOptions } from '$features/projects/components/table/config-options.svelte';
    import ProjectsConfigDataTable from '$features/projects/components/table/projects-config-data-table.svelte';
    import { ClientConfigurationSetting } from '$features/projects/models';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { toast } from 'svelte-sonner';

    const projectId = page.params.projectId || '';
    const projectConfigQuery = getProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    let showAddProjectConfigDialog = $state(false);
    const newConfigurationSetting = postProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    async function addClientConfigurationSetting(setting: ClientConfigurationSetting) {
        try {
            await newConfigurationSetting.mutateAsync(setting);
            toast.success(`Successfully added ${setting.key} setting.`);
        } catch {
            toast.error(`Error adding ${setting.key}'s setting. Please try again.`);
        }
    }

    const DEFAULT_PARAMS = {
        limit: DEFAULT_LIMIT
    };

    const queryParams = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number'
        }
    });

    const inMemoryQueryParameters = {
        get limit() {
            return queryParams.limit!;
        },
        get projectId() {
            return projectId;
        }
    };

    const table = createTable(getTableOptions<ClientConfigurationSetting>(inMemoryQueryParameters, inMemoryQueryParameters, projectConfigQuery));

    $effect(() => {
        // Handle case where pop state loses the limit
        queryParams.limit ??= DEFAULT_LIMIT;
    });

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <div class="flex items-start justify-between">
        <div>
            <H3>Configuration Values</H3>
            <Muted
                >The <A href="https://exceptionless.com/docs/project-settings/#client-configuration" target="_blank">configuration value</A> will be sent to the
                Exceptionless clients in real time. This allows you to change how your app works without redeploying your app.</Muted
            >
        </div>

        <Button size="icon" onclick={() => (showAddProjectConfigDialog = true)} title="Add Configuration Value" class="shrink-0">
            <Plus class="size-4" aria-hidden="true" />
            <span class="sr-only">Add Configuration Value</span>
        </Button>
    </div>
    <Separator />

    <ProjectsConfigDataTable bind:limit={queryParams.limit!} isLoading={projectConfigQuery.isLoading} {table} />
</div>

{#if showAddProjectConfigDialog}
    <AddProjectConfigDialog bind:open={showAddProjectConfigDialog} save={addClientConfigurationSetting} />
{/if}
