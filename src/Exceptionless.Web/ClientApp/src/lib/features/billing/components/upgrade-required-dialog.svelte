<script lang="ts">
    import { toast } from 'svelte-sonner';

    import { Muted } from '#comp/typography/index.ts';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { isStripeEnabled } from '#features/billing/index.ts';

    import { showChangePlanDialog } from '../change-plan.svelte';
    import { upgradeRequiredDialog } from '../upgrade-required.svelte';

    const canOpenBilling = $derived(isStripeEnabled() && !!upgradeRequiredDialog.organizationId);

    function handleOpenChange(open: boolean) {
        if (!open) {
            onCancel();
        }
    }

    function onCancel() {
        upgradeRequiredDialog.reset();
    }

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
