<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { addOrganizationUser, removeOrganizationUser } from '$features/organizations/api.svelte';
    import { ViewUser } from '$features/users/models';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import EllipsisIcon from '@lucide/svelte/icons/ellipsis';
    import Mail from '@lucide/svelte/icons/mail';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';

    import RemoveUserDialog from '../dialogs/remove-user-dialog.svelte';

    interface Props {
        organizationId: string;
        user: ViewUser;
    }

    let { organizationId, user }: Props = $props();
    let showRemoveUserDialog = $state(false);
    let toastId = $state<number | string | undefined>();

    const removeUser = removeOrganizationUser({
        route: {
            email: user.email_address,
            organizationId
        }
    });

    const addUser = addOrganizationUser({
        route: {
            email: user.email_address,
            organizationId
        }
    });

    async function remove() {
        toast.dismiss(toastId);

        try {
            await removeUser.mutateAsync();
            toastId = toast.success('Successfully removed the user from the organization.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to remove the user: ${message}`);
            throw error;
        }
    }

    async function resendInviteEmail() {
        toast.dismiss(toastId);

        try {
            await addUser.mutateAsync();
            toastId = toast.success('Successfully resent the invite email.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to resend the invite: ${message}`);
            throw error;
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
        {#if user.is_invite}
            <DropdownMenu.Item onclick={() => resendInviteEmail()} disabled={addUser.isPending}>
                <Mail />
                Resend Invite Email
            </DropdownMenu.Item>
        {/if}
        <DropdownMenu.Item onclick={() => (showRemoveUserDialog = true)} disabled={removeUser.isPending}>
            <X />
            {#if user.is_invite}
                Revoke Invite
            {:else}
                Remove User
            {/if}
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if showRemoveUserDialog}
    <RemoveUserDialog bind:open={showRemoveUserDialog} name={user.full_name ?? user.email_address} {remove} />
{/if}
