<script lang="ts">
    import type { ClientConfigurationSetting } from '$features/projects/models';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteProjectConfig, postProjectConfig } from '$features/projects/api.svelte';
    import EllipsisIcon from '@lucide/svelte/icons/ellipsis';
    import Edit from '@lucide/svelte/icons/pen';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';

    import RemoveProjectConfigDialog from '../dialogs/remove-project-config-dialog.svelte';
    import UpdateProjectConfigDialog from '../dialogs/update-project-config-dialog.svelte';

    interface Props {
        projectId: string;
        setting: ClientConfigurationSetting;
    }

    let { projectId, setting }: Props = $props();

    let toastId = $state<number | string>();
    let showUpdateProjectConfigDialog = $state(false);
    let showRemoveProjectConfigDialog = $state(false);

    const updateProjectConfig = postProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    const removeProjectConfig = deleteProjectConfig({
        route: {
            get id() {
                return projectId;
            }
        }
    });

    async function save(value: string) {
        toast.dismiss(toastId);

        try {
            await updateProjectConfig.mutateAsync({ key: setting.key, value });
            toastId = toast.success(`Successfully updated ${setting.key} setting.`);
        } catch {
            toastId = toast.error(`Error updating ${setting.key}'s setting. Please try again.`);
        }
    }

    async function remove() {
        toast.dismiss(toastId);

        try {
            await removeProjectConfig.mutateAsync({ key: setting.key });
            toastId = toast.success(`Successfully removed ${setting.key} setting.`);
        } catch {
            toastId = toast.error(`Error removing ${setting.key}'s setting. Please try again.`);
        }
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
        <DropdownMenu.Item onclick={() => (showUpdateProjectConfigDialog = true)} disabled={updateProjectConfig.isPending}>
            <Edit />
            Edit
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => (showRemoveProjectConfigDialog = true)} disabled={removeProjectConfig.isPending}>
            <X />
            Delete
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if showUpdateProjectConfigDialog}
    <UpdateProjectConfigDialog bind:open={showUpdateProjectConfigDialog} {setting} {save} />
{/if}
{#if showRemoveProjectConfigDialog}
    <RemoveProjectConfigDialog bind:open={showRemoveProjectConfigDialog} name={setting.key} {remove} />
{/if}
