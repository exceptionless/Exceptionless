<script module lang="ts">
    export type ProjectActionId = 'client-setup' | 'generate-sample-data' | 'notifications' | 'open' | 'reset-data' | 'stacks';
</script>

<script lang="ts">
    import type { ViewProject } from '$features/projects/models';
    import type { Component } from 'svelte';

    import { resolve } from '$app/paths';
    import * as Command from '$comp/ui/command';
    import { organization } from '$features/organizations/context.svelte';
    import { generateSampleData, getOrganizationProjectsQuery } from '$features/projects/api.svelte';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import ArrowLeft from '@lucide/svelte/icons/arrow-left';
    import Bell from '@lucide/svelte/icons/bell';
    import CloudDownload from '@lucide/svelte/icons/cloud-download';
    import Database from '@lucide/svelte/icons/database';
    import FolderOpen from '@lucide/svelte/icons/folder-open';
    import Stacks from '@lucide/svelte/icons/layers';
    import { toast } from 'svelte-sonner';

    type ProjectAction = {
        icon: Component;
        id: ProjectActionId;
        keywords: string[];
        label: string;
    };

    interface Props {
        onReset: (project: ViewProject) => void;
        onSearchReset: () => void;
        onSelect: () => void;
        open: boolean;
        resetPending: boolean;
        selectedActionId: ProjectActionId | undefined;
        selectingProject: boolean;
    }

    let { onReset, onSearchReset, onSelect, open, resetPending, selectedActionId = $bindable(), selectingProject = $bindable() }: Props = $props();

    const projectActions: ProjectAction[] = [
        { icon: FolderOpen, id: 'open', keywords: ['edit', 'manage', 'settings'], label: 'Open Project' },
        { icon: Stacks, id: 'stacks', keywords: ['errors', 'exceptions', 'issues'], label: 'Project Stacks' },
        { icon: Bell, id: 'notifications', keywords: ['alerts', 'email'], label: 'Project Notifications' },
        { icon: CloudDownload, id: 'client-setup', keywords: ['SDK', 'configure client', 'instrumentation'], label: 'Client Setup' },
        { icon: Database, id: 'generate-sample-data', keywords: ['seed', 'demo events'], label: 'Generate Sample Data' },
        { icon: AlertTriangle, id: 'reset-data', keywords: ['clear', 'delete events'], label: 'Reset Project Data' }
    ];

    const selectedAction = $derived(projectActions.find((action) => action.id === selectedActionId));

    const projectsQuery = getOrganizationProjectsQuery({
        enabled: () => open,
        route: {
            get organizationId() {
                return organization.current;
            }
        }
    });
    const projects = $derived([...(projectsQuery.data?.data ?? [])].sort((a, b) => a.name.localeCompare(b.name)));

    let sampleDataProjectId = $state<string>();
    const generateSampleDataMutation = generateSampleData({
        route: {
            get id() {
                return sampleDataProjectId;
            }
        }
    });

    function selectAction(action: ProjectAction): void {
        selectedActionId = action.id;
        selectingProject = true;
        onSearchReset();
    }

    function goBack(): void {
        selectedActionId = undefined;
        selectingProject = false;
        onSearchReset();
    }

    function getProjectHref(action: ProjectActionId, project: ViewProject): string | undefined {
        switch (action) {
            case 'client-setup':
                return resolve('/(app)/project/[projectId]/configure', { projectId: project.id });
            case 'notifications':
                return `${resolve('/(app)/account/notifications')}?project=${project.id}`;
            case 'open':
                return resolve('/(app)/project/[projectId]/manage', { projectId: project.id });
            case 'stacks':
                return `${resolve('/(app)/stack')}?filter=project:${project.id}`;
            default:
                return undefined;
        }
    }

    function closeForProject(project: ViewProject): void {
        organization.current = project.organization_id;
        onSelect();
    }

    async function generateProjectSampleData(project: ViewProject): Promise<void> {
        closeForProject(project);
        sampleDataProjectId = project.id;

        try {
            await generateSampleDataMutation.mutateAsync();
            toast.success(`Sample data generation has been queued for "${project.name}". Events will appear shortly.`);
        } catch {
            toast.error(`Failed to generate sample data for "${project.name}". Please try again.`);
        } finally {
            sampleDataProjectId = undefined;
        }
    }

    function openResetProjectDataDialog(project: ViewProject): void {
        closeForProject(project);
        onReset(project);
    }

    function selectProject(project: ViewProject): void {
        if (!selectedAction) {
            return;
        }

        if (selectedAction.id === 'generate-sample-data') {
            void generateProjectSampleData(project);
        } else if (selectedAction.id === 'reset-data') {
            openResetProjectDataDialog(project);
        }
    }
</script>

{#if organization.current}
    {#if selectingProject && selectedAction}
        <Command.Group heading={selectedAction.label} value={`${selectedAction.label} Select Project`}>
            <Command.Item onSelect={goBack} value="Back to commands">
                <ArrowLeft />
                <span>Back to commands</span>
            </Command.Item>
            {#if projectsQuery.isLoading}
                <Command.Item disabled value="Loading projects">
                    <Database />
                    <span>Loading projects...</span>
                </Command.Item>
            {:else if projectsQuery.isError}
                <Command.Item disabled value="Unable to load projects">
                    <AlertTriangle />
                    <span>Unable to load projects</span>
                </Command.Item>
            {:else if projects.length === 0}
                <Command.Item disabled value="No projects available">
                    <Database />
                    <span>No projects available</span>
                </Command.Item>
            {:else}
                {#each projects as project (project.id)}
                    {@const href = getProjectHref(selectedAction.id, project)}
                    {@const Icon = selectedAction.icon}
                    {#if href}
                        <Command.LinkItem {href} onclick={() => closeForProject(project)} value={`${selectedAction.label} ${project.name} ${project.id}`}>
                            <Icon />
                            <div class="flex min-w-0 flex-col">
                                <span class="truncate">{project.name}</span>
                                <span class="text-muted-foreground truncate text-xs">{selectedAction.label}</span>
                            </div>
                        </Command.LinkItem>
                    {:else}
                        <Command.Item
                            disabled={generateSampleDataMutation.isPending || resetPending}
                            onSelect={() => selectProject(project)}
                            value={`${selectedAction.label} ${project.name} ${project.id}`}
                        >
                            <Icon />
                            <div class="flex min-w-0 flex-col">
                                <span class="truncate">{project.name}</span>
                                <span class="text-muted-foreground truncate text-xs">{selectedAction.label}</span>
                            </div>
                        </Command.Item>
                    {/if}
                {/each}
            {/if}
        </Command.Group>
    {:else}
        <Command.Group heading="Project Actions" value="Project Actions">
            {#each projectActions as action (action.id)}
                {@const Icon = action.icon}
                <Command.Item keywords={action.keywords} onSelect={() => selectAction(action)} value={`Project Actions ${action.label}`}>
                    <Icon />
                    <span>{action.label}</span>
                </Command.Item>
            {/each}
        </Command.Group>
        <Command.Separator />
    {/if}
{/if}
