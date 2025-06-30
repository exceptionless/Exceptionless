<script lang="ts">
    import { goto } from '$app/navigation';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteProject } from '$features/projects/api.svelte';
    import { ViewProject } from '$features/projects/models';
    import Issues from '@lucide/svelte/icons/bug';
    import Configure from '@lucide/svelte/icons/cloud';
    import EllipsisIcon from '@lucide/svelte/icons/ellipsis';
    import Organization from '@lucide/svelte/icons/group';
    import Edit from '@lucide/svelte/icons/pen';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';

    import RemoveProjectDialog from '../dialogs/remove-project-dialog.svelte';

    interface Props {
        project: ViewProject;
    }

    let { project }: Props = $props();
    let showRemoveProjectDialog = $state(false);

    const removeProject = deleteProject({
        route: {
            get ids() {
                return [project.id!];
            }
        }
    });

    async function remove() {
        await removeProject.mutateAsync();
        toast.success('Successfully queued the project for deletion.');
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="ghost" size="icon" class="relative size-8 p-0">
                <span class="sr-only">Open menu</span>
                <EllipsisIcon />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end">
        <DropdownMenu.Item onclick={() => goto(`/next/issues`)}>
            <Issues />
            Issues
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => goto(`/next/project/${project.id}/manage`)}>
            <Edit />
            Edit
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => goto(`/next/project/${project.id}/configure`)}>
            <Configure />
            Download & Configure Client
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => (showRemoveProjectDialog = true)} disabled={removeProject.isPending}>
            <X />
            Delete
        </DropdownMenu.Item>
        <DropdownMenu.Separator />
        <DropdownMenu.Item onclick={() => goto(`/organization/${project.organization_id}/manage`)}>
            <Organization />
            View Organization
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if showRemoveProjectDialog}
    <RemoveProjectDialog bind:open={showRemoveProjectDialog} name={project.name} {remove} />
{/if}
