<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import * as AlertDialog from '$comp/ui/alert-dialog';

    import { upgradeRequiredDialog } from '../upgrade-required.svelte';

    function onUpgrade() {
        const orgId = upgradeRequiredDialog.organizationId;
        upgradeRequiredDialog.open = false;

        if (orgId) {
            void goto(resolve('/(app)/organization/[organizationId]/billing', { organizationId: orgId }) + '?changePlan=true');
        }
    }
</script>

<AlertDialog.Root bind:open={upgradeRequiredDialog.open}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Upgrade Plan</AlertDialog.Title>
            <AlertDialog.Description>{upgradeRequiredDialog.message}</AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action onclick={onUpgrade}>Upgrade Plan</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
