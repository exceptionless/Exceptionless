<script lang="ts">
    import type { OAuthApplication } from '$features/admin/models';

    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteOAuthApplicationMutation } from '$features/admin/api.svelte';
    import EllipsisIcon from '@lucide/svelte/icons/ellipsis';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import { toast } from 'svelte-sonner';

    interface Props {
        application: OAuthApplication;
    }

    let { application }: Props = $props();

    const deleteApplication = deleteOAuthApplicationMutation();
    let deleteDialogOpen = $state(false);

    async function deleteOAuthApplication() {
        try {
            await deleteApplication.mutateAsync(application.id);
            deleteDialogOpen = false;
            toast.success('OAuth application deleted.');
        } catch {
            toast.error('Failed to delete OAuth application.');
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="ghost" size="icon" class="relative size-8 p-0">
                <span class="sr-only">Open menu for {application.name}</span>
                <EllipsisIcon class="size-4" aria-hidden="true" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end">
        <DropdownMenu.Item variant="destructive" onclick={() => (deleteDialogOpen = true)} disabled={deleteApplication.isPending}>
            <Trash2 class="size-4" aria-hidden="true" />
            Delete
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

<AlertDialog.Root bind:open={deleteDialogOpen}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Delete OAuth Application</AlertDialog.Title>
            <AlertDialog.Description>
                Delete "{application.name}"? Disable the client instead when you only need to block OAuth access.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action
                class={buttonVariants({
                    variant: 'destructive'
                })}
                disabled={deleteApplication.isPending}
                onclick={() => void deleteOAuthApplication()}
            >
                Delete
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
