<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button } from '$comp/ui/button';

    interface Props {
        onDiscard: () => void;
        onSave: () => Promise<void> | void;
        onStay: () => void;
        open: boolean;
        saving: boolean;
    }

    let { onDiscard, onSave, onStay, open = $bindable(), saving }: Props = $props();

    function handleOpenChange(nextOpen: boolean): void {
        if (!nextOpen) {
            onStay();
        }
    }
</script>

<AlertDialog.Root bind:open onOpenChange={handleOpenChange}>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Save changes to this view?</AlertDialog.Title>
            <AlertDialog.Description>Your changes will be lost if you leave without saving.</AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel disabled={saving}>Cancel</AlertDialog.Cancel>
            <Button variant="outline" onclick={onDiscard} disabled={saving}>Don't save</Button>
            <Button onclick={() => void onSave()} disabled={saving}>Save</Button>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
