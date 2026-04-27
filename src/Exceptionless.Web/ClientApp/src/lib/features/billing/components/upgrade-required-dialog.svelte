<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { ChangePlanDialog, isStripeEnabled } from '$features/billing';
    import { getOrganizationQuery } from '$features/organizations/api.svelte';

    import { upgradeRequiredDialog } from '../upgrade-required.svelte';

    let showChangePlan = $state(false);

    const organizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return showChangePlan ? upgradeRequiredDialog.organizationId : undefined;
            }
        }
    });

    function onUpgrade() {
        upgradeRequiredDialog.open = false;

        if (isStripeEnabled() && upgradeRequiredDialog.organizationId) {
            showChangePlan = true;
        }
    }

    async function onChangePlanClose(success: boolean) {
        const retry = success ? upgradeRequiredDialog.retryCallback : undefined;
        showChangePlan = false;
        upgradeRequiredDialog.reset();

        if (retry) {
            await retry();
        }
    }

    function onCancel() {
        upgradeRequiredDialog.reset();
        showChangePlan = false;
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
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel onclick={onCancel}>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action onclick={onUpgrade}>Upgrade Plan</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>

{#if showChangePlan && organizationQuery.data}
    <ChangePlanDialog onclose={onChangePlanClose} organization={organizationQuery.data} />
{/if}
