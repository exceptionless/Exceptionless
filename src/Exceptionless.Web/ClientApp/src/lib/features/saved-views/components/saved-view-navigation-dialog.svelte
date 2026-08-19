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
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content class="sm:w-[28rem] data-[size=default]:sm:max-w-none">
        <AlertDialog.Header>
            <AlertDialog.Title>Save view changes?</AlertDialog.Title>
            <AlertDialog.Description>You changed this view's settings. Save the changes before leaving, or discard them and continue?</AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer class="sm:flex-wrap">
            <AlertDialog.Cancel onclick={onStay} disabled={saving}>Stay here</AlertDialog.Cancel>
            <Button variant="outline" onclick={onDiscard} disabled={saving}>Discard changes</Button>
            <Button onclick={() => void onSave()} disabled={saving}>Save and continue</Button>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
