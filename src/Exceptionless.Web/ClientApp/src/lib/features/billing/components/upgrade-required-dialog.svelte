<script lang="ts">
    import { Muted } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { isStripeEnabled } from '$features/billing';
    import { toast } from 'svelte-sonner';

    import { showChangePlanDialog } from '../change-plan.svelte';
    import { upgradeRequiredDialog } from '../upgrade-required.svelte';

    const canOpenBilling = $derived(isStripeEnabled() && !!upgradeRequiredDialog.organizationId);

    function onUpgrade() {
        const organizationId = upgradeRequiredDialog.organizationId;
        if (!isStripeEnabled() || !organizationId) {
            toast.error('Billing is not configured in this environment. Contact your administrator.');
            return;
        }

        const onSuccess = upgradeRequiredDialog.retryCallback;
        upgradeRequiredDialog.reset();
        showChangePlanDialog(organizationId, {
            onSuccess
        });
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

<AlertDialog.Root open={upgradeRequiredDialog.open} onOpenChange={handleOpenChange}>
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
