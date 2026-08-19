<script lang="ts">
    import ErrorMessage from '$comp/error-message.svelte';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Spinner } from '$comp/ui/spinner';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';

    import { changePlanDialog } from '../change-plan.svelte';
    import ChangePlanDialog from './change-plan-dialog.svelte';

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return changePlanDialog.open ? changePlanDialog.organizationId : undefined;
            }
        }
    });

    async function onChangePlanClose(success: boolean): Promise<void> {
        const onSuccess = success ? changePlanDialog.onSuccess : undefined;
        changePlanDialog.reset();

        if (onSuccess) {
            await onSuccess();
        }
    }

    function onOpenChange(open: boolean): void {
        if (!open) {
            changePlanDialog.reset();
        }
    }

    async function retry(): Promise<void> {
        await organizationQuery.refetch();
    }
</script>

{#if changePlanDialog.open && organizationQuery.data}
    <ChangePlanDialog initialPlanId={changePlanDialog.initialPlanId} onclose={onChangePlanClose} organization={organizationQuery.data} />
{:else}
    <Dialog.Root open={changePlanDialog.open} {onOpenChange}>
        <Dialog.Content class="sm:max-w-md">
            <Dialog.Header>
                <Dialog.Title>Manage subscription</Dialog.Title>
                <Dialog.Description>
                    {organizationQuery.error ? 'We couldn’t load this organization’s billing details.' : 'Loading billing details…'}
                </Dialog.Description>
            </Dialog.Header>
            {#if organizationQuery.error}
                <ErrorMessage message="Failed to load billing details. Please try again." />
            {:else}
                <div class="flex justify-center py-4">
                    <Spinner aria-label="Loading billing details" />
                </div>
            {/if}
            <Dialog.Footer>
                <Button type="button" variant="outline" onclick={() => changePlanDialog.reset()}>Cancel</Button>
                {#if organizationQuery.error}
                    <Button type="button" disabled={organizationQuery.isFetching} onclick={retry}>
                        {organizationQuery.isFetching ? 'Retrying…' : 'Retry'}
                    </Button>
                {/if}
            </Dialog.Footer>
        </Dialog.Content>
    </Dialog.Root>
{/if}
