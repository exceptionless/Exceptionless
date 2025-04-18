<script lang="ts">
    import { page } from '$app/state';
    import * as DataTable from '$comp/data-table';
    import { A, H3, Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import { Separator } from '$comp/ui/separator';
    import { getProjectConfig, postProjectConfig } from '$features/projects/api.svelte';
    import AddProjectConfigDialog from '$features/projects/components/dialogs/add-project-config-dialog.svelte';
    import { getTableContext } from '$features/projects/components/table/config-options.svelte';
    import ProjectsConfigDataTable from '$features/projects/components/table/projects-config-data-table.svelte';
    import { ClientConfigurationSetting } from '$features/projects/models';
    import { DEFAULT_LIMIT } from '$features/shared/api/api.svelte';
    import Plus from '@lucide/svelte/icons/plus';
    import { createTable } from '@tanstack/svelte-table';
    import { queryParamsState } from 'kit-query-params';
    import { watch } from 'runed';
    import { toast } from 'svelte-sonner';

    const projectId = page.params.projectId || '';
    const projectConfigResponse = getProjectConfig({
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

    const params = queryParamsState({
        default: DEFAULT_PARAMS,
        pushHistory: true,
        schema: {
            limit: 'number'
        }
    });

    const contextParameters = {
        get limit() {
            return params.limit!;
        },
        get projectId() {
            return projectId;
        }
    };

    // TODO: Fix paging
    const context = getTableContext<ClientConfigurationSetting>(contextParameters);
    const table = createTable(context.options);

    const knownSettingsToHide = ['@@log:*', '@@DataExclusions', '@@IncludePrivateInformation', '@@UserAgentBotPatterns', 'UserNamespaces', 'CommonMethods'];
    watch(
        () => projectConfigResponse.dataUpdatedAt,
        () => {
            if (projectConfigResponse.isSuccess) {
                context.data = Object.entries(projectConfigResponse.data.settings)
                    .map(([key, value]) => {
                        const config = new ClientConfigurationSetting();
                        config.key = key;
                        config.value = value;
                        return config;
                    })
                    .filter((setting) => !knownSettingsToHide.includes(setting.key));
            }
        }
    );

    $effect(() => {
        // Handle case where pop state loses the limit
        params.limit ??= DEFAULT_LIMIT;
    });

    // TODO: Add Skeleton
</script>

<div class="space-y-6">
    <div>
        <H3>Configuration Values</H3>
        <Muted
            >The <A href="https://exceptionless.com/docs/project-settings/#client-configuration" target="_blank">configuration value</A> will be sent to the Exceptionless
            clients in real time. This allows you to change how your app works without redeploying your app.</Muted
        >
    </div>
    <Separator />

    <ProjectsConfigDataTable bind:limit={params.limit!} isLoading={projectConfigResponse.isLoading} {table}>
        {#snippet footerChildren()}
            <div class="h-9 min-w-[140px]">
                <Button size="sm" onclick={() => (showAddProjectConfigDialog = true)}>
                    <Plus class="mr-2 size-4" />
                    Add Configuration Value</Button
                >
            </div>

            <DataTable.PageSize bind:value={params.limit!} {table}></DataTable.PageSize>
            <div class="flex items-center space-x-6 lg:space-x-8">
                <DataTable.PageCount {table} />
                <DataTable.Pagination {table} />
            </div>
        {/snippet}
    </ProjectsConfigDataTable>
</div>

{#if showAddProjectConfigDialog}
    <AddProjectConfigDialog bind:open={showAddProjectConfigDialog} save={addClientConfigurationSetting} />
{/if}
