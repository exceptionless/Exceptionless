<script lang="ts">
    import { goto } from '$app/navigation';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteOrganization, deleteOrganizationUser } from '$features/organizations/api.svelte';
    import { ViewOrganization } from '$features/organizations/models';
    import { getMeQuery } from '$features/users/api.svelte';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import ChangePlan from '@lucide/svelte/icons/credit-card';
    import EllipsisIcon from '@lucide/svelte/icons/ellipsis';
    import ViewInvoices from '@lucide/svelte/icons/file-text';
    import LeaveOrganization from '@lucide/svelte/icons/log-out';
    import Edit from '@lucide/svelte/icons/pen';
    import X from '@lucide/svelte/icons/x';
    import { toast } from 'svelte-sonner';

    import LeaveOrganizationDialog from '../dialogs/leave-organization-dialog.svelte';
    import RemoveOrganizationDialog from '../dialogs/remove-organization-dialog.svelte';

    interface Props {
        organization: ViewOrganization;
    }

    let { organization: org }: Props = $props();

    let showRemoveOrganizationDialog = $state(false);
    let showLeaveOrganizationDialog = $state(false);
    let toastId = $state<number | string>();

    const meQuery = getMeQuery();

    const removeOrganization = deleteOrganization({
        route: {
            get ids() {
                return [org.id!];
            }
        }
    });

    const leaveOrganization = deleteOrganizationUser({
        route: {
            get email() {
                return meQuery.data?.email_address ?? '';
            },
            get organizationId() {
                return org.id!;
            }
        }
    });

    async function remove() {
        toast.dismiss(toastId);
        try {
            await removeOrganization.mutateAsync();
            toastId = toast.success('Successfully queued the organization for deletion.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to delete the organization: ${message}`);
        }
    }

    async function leave() {
        toast.dismiss(toastId);
        try {
            await leaveOrganization.mutateAsync();
            toastId = toast.success('Successfully removed the user from the organization.');
        } catch (error: unknown) {
            console.log(error);
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to leave the organization: ${message}`);
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
        <DropdownMenu.Item onclick={() => goto(`/next/organization/${org.id}/manage`)}>
            <Edit />
            Edit
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => goto(`/next/organization/${org.id}/billing`)}>
            <ChangePlan />
            Change Plan
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => goto(`/next/organization/${org.id}/billing`)}>
            <ViewInvoices />
            View Invoices
        </DropdownMenu.Item>
        <DropdownMenu.Separator />
        <DropdownMenu.Item onclick={() => (showLeaveOrganizationDialog = true)} disabled={leaveOrganization.isPending}>
            <LeaveOrganization />
            Leave Organization
        </DropdownMenu.Item>
        <DropdownMenu.Item onclick={() => (showRemoveOrganizationDialog = true)} disabled={removeOrganization.isPending}>
            <X />
            Delete
        </DropdownMenu.Item>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if showRemoveOrganizationDialog}
    <RemoveOrganizationDialog bind:open={showRemoveOrganizationDialog} name={org.name} {remove} />
{/if}

{#if showLeaveOrganizationDialog}
    <LeaveOrganizationDialog bind:open={showLeaveOrganizationDialog} name={org.name} {leave} />
{/if}
