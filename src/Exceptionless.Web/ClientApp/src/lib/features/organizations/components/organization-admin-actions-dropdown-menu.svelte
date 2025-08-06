<script lang="ts">
    import type { PostSetBonusOrganizationParams, PostSuspendOrganizationParams } from '$features/organizations/api.svelte';
    import type { ViewOrganization } from '$features/organizations/models';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { deleteSuspendOrganization, postSetBonusOrganization, postSuspendOrganization } from '$features/organizations/api.svelte';
    import SetEventBonusDialog from '$features/organizations/components/dialogs/set-event-bonus-dialog.svelte';
    import SuspendOrganizationDialog from '$features/organizations/components/dialogs/suspend-organization-dialog.svelte';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Award from '@lucide/svelte/icons/award';
    import Pause from '@lucide/svelte/icons/pause';
    import Play from '@lucide/svelte/icons/play';
    import Shield from '@lucide/svelte/icons/shield';
    import { toast } from 'svelte-sonner';

    interface Props {
        organization: undefined | ViewOrganization;
    }

    let { organization }: Props = $props();

    let toastId = $state<number | string>();
    let openSuspendOrganizationDialog = $state(false);
    let openSetEventBonusDialog = $state(false);

    const markSuspended = postSuspendOrganization({
        route: {
            get id() {
                return organization?.id;
            }
        }
    });

    const markUnsuspended = deleteSuspendOrganization({
        route: {
            get id() {
                return organization?.id;
            }
        }
    });

    const setOrganizationBonus = postSetBonusOrganization();

    async function suspend(params: PostSuspendOrganizationParams) {
        toast.dismiss(toastId);

        try {
            await markSuspended.mutateAsync(params);
            toastId = toast.success('Successfully suspended the organization.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to suspend the organization: ${message}`);
        }
    }

    async function setBonus(params: PostSetBonusOrganizationParams) {
        toast.dismiss(toastId);

        try {
            await setOrganizationBonus.mutateAsync(params);
            toastId = toast.success('Successfully set the organization bonus.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to set the organization bonus: ${message}`);
        }
    }

    async function handleUnsuspend() {
        toast.dismiss(toastId);

        try {
            await markUnsuspended.mutateAsync();
            toastId = toast.success('Successfully unsuspended the organization.');
        } catch (error: unknown) {
            const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
            toastId = toast.error(`An error occurred while trying to unsuspend the organization: ${message}`);
        }
    }

    function handleSetBonus() {
        openSetEventBonusDialog = true;
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        <Button variant="outline" size="icon" aria-label="Admin Actions" title="Admin Actions">
            <Shield class="size-4" />
        </Button>
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end" class="w-56">
        <DropdownMenu.Group>
            <DropdownMenu.GroupHeading>Admin Actions</DropdownMenu.GroupHeading>
            <DropdownMenu.Separator />
            {#if organization?.is_suspended}
                <DropdownMenu.Item onclick={handleUnsuspend} disabled={markUnsuspended.isPending}>
                    <Play class="mr-2 size-4" />
                    <span>Unsuspend Organization</span>
                </DropdownMenu.Item>
            {:else}
                <DropdownMenu.Item onclick={() => (openSuspendOrganizationDialog = true)} disabled={markSuspended.isPending}>
                    <Pause class="mr-2 size-4" />
                    <span>Suspend Organization</span>
                </DropdownMenu.Item>
            {/if}
            <DropdownMenu.Separator />
            <DropdownMenu.Item onclick={handleSetBonus}>
                <Award class="mr-2 size-4" />
                <span>Set Bonus</span>
            </DropdownMenu.Item>
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if organization && openSuspendOrganizationDialog}
    <SuspendOrganizationDialog bind:open={openSuspendOrganizationDialog} {organization} {suspend} />
{/if}

{#if organization && openSetEventBonusDialog}
    <SetEventBonusDialog bind:open={openSetEventBonusDialog} {organization} {setBonus} />
{/if}
