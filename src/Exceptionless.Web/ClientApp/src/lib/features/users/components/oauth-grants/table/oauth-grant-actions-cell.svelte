<script lang="ts">
    import type { OAuthGrant } from '$features/users/models';

    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button, buttonVariants } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteOAuthGrantMutation } from '$features/users/api.svelte';
    import EllipsisIcon from '@lucide/svelte/icons/ellipsis';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import { toast } from 'svelte-sonner';

    interface Props {
        grant: OAuthGrant;
    }

    let { grant }: Props = $props();

    const revokeGrant = deleteOAuthGrantMutation();

    let revokeDialogOpen = $state(false);
    let toastId = $state<number | string>();

    async function revokeAccess() {
        toast.dismiss(toastId);
        try {
            await revokeGrant.mutateAsync(grant.id);
            toastId = toast.success('Application access revoked.');
            revokeDialogOpen = false;
        } catch {
            toastId = toast.error('Error revoking application access. Please try again.');
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="ghost" size="icon" class="relative size-8 p-0">
                <span class="sr-only">Open menu</span>
                <EllipsisIcon class="size-4" aria-hidden="true" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end">
        <DropdownMenu.Item onclick={() => (revokeDialogOpen = true)} disabled={revokeGrant.isPending}>
            <Trash2 class="size-4" aria-hidden="true" />
            Revoke Access
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

<AlertDialog.Root bind:open={revokeDialogOpen}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Revoke Application Access</AlertDialog.Title>
            <AlertDialog.Description>
                Revoke access for "{grant.application_name}"? The application will need to complete OAuth again before it can access your account.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} disabled={revokeGrant.isPending} onclick={() => void revokeAccess()}>
                Revoke Access
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
