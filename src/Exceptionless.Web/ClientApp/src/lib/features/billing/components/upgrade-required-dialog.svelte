<script lang="ts">
    import { Muted } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { ChangePlanDialog, isStripeEnabled } from '$features/billing';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';
    import { toast } from 'svelte-sonner';

    import { upgradeRequiredDialog } from '../upgrade-required.svelte';

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return upgradeRequiredDialog.open && upgradeRequiredDialog.step === 'plan-picker' ? upgradeRequiredDialog.organizationId : undefined;
            }
        }
    });

    const canOpenBilling = $derived(isStripeEnabled() && !!upgradeRequiredDialog.organizationId);

    function onUpgrade() {
        if (!canOpenBilling) {
            toast.error('Billing is not configured in this environment. Contact your administrator.');
            return;
        }

        upgradeRequiredDialog.showPlanPicker();
    }

    async function onChangePlanClose(success: boolean) {
        const onSuccess = success ? upgradeRequiredDialog.onSuccess : undefined;
        upgradeRequiredDialog.reset();

        if (onSuccess) {
            await onSuccess();
        }
    }

    function onCancel() {
        upgradeRequiredDialog.reset();
    }

    function handleOpenChange(open: boolean) {
        if (!open) {
            onCancel();
        }
    }
</script>

<AlertDialog.Root open={upgradeRequiredDialog.open && upgradeRequiredDialog.step === 'confirmation'} onOpenChange={handleOpenChange}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Upgrade Plan</AlertDialog.Title>
            <AlertDialog.Description>{upgradeRequiredDialog.message}</AlertDialog.Description>
            {#if !canOpenBilling}
                <Muted>Billing checkout is unavailable in this environment.</Muted>
            {/if}
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel onclick={onCancel}>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action onclick={onUpgrade} disabled={!canOpenBilling}>Upgrade Plan</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>

{#if upgradeRequiredDialog.open && upgradeRequiredDialog.step === 'plan-picker' && organizationQuery.data}
    <ChangePlanDialog initialTierId={upgradeRequiredDialog.initialTierId} onclose={onChangePlanClose} organization={organizationQuery.data} />
{:else if upgradeRequiredDialog.open && upgradeRequiredDialog.step === 'plan-picker'}
    <AlertDialog.Root open={true} onOpenChange={handleOpenChange}>
        <AlertDialog.Content>
            <AlertDialog.Header>
                <AlertDialog.Title>Upgrade Plan</AlertDialog.Title>
                <AlertDialog.Description>
                    {#if organizationQuery.isFetching}Loading billing details…{:else}We couldn’t load this organization’s billing details.{/if}
                </AlertDialog.Description>
            </AlertDialog.Header>
            <AlertDialog.Footer>
                <AlertDialog.Cancel onclick={onCancel}>Cancel</AlertDialog.Cancel>
                {#if organizationQuery.error}
                    <AlertDialog.Action disabled={organizationQuery.isFetching} onclick={() => void organizationQuery.refetch()}>
                        {organizationQuery.isFetching ? 'Retrying…' : 'Retry'}
                    </AlertDialog.Action>
                {/if}
            </AlertDialog.Footer>
        </AlertDialog.Content>
    </AlertDialog.Root>
{/if}
