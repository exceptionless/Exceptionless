<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { removeOrganizationUser } from '$features/organizations/api.svelte';
    import { resendVerificationEmail } from '$features/users/api.svelte';
    import { ViewUser } from '$features/users/models';
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

    const removeUser = removeOrganizationUser({
        route: {
            email: user.email_address,
            organizationId
        }
    });

    const resendEmail = resendVerificationEmail({
        route: {
            get id() {
                return user.id!;
            }
        }
    });

    async function remove() {
        await removeUser.mutateAsync();
        toast.success('Successfully removed the user from the organization.');
    }

    async function resendInviteEmail() {
        await resendEmail.mutateAsync();
        toast.success('Successfully resent the invite email.');
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
            <DropdownMenu.Item onclick={() => resendInviteEmail()} disabled={resendEmail.isPending}>
                <Mail />
                Resend Invite Email
            </DropdownMenu.Item>
        {/if}
        <DropdownMenu.Item onclick={() => (showRemoveUserDialog = true)} disabled={removeUser.isPending}>
            <X />
            Remove User
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if showRemoveUserDialog}
    <RemoveUserDialog bind:open={showRemoveUserDialog} name={user.full_name} {remove} />
{/if}
