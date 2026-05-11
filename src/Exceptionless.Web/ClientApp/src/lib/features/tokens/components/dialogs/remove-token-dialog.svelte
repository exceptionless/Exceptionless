<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        id: string;
        notes?: null | string;
        open: boolean;
        remove: () => Promise<void>;
    }

    let { id, notes, open = $bindable(false), remove }: Props = $props();

    async function onSubmit() {
        await remove();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Delete API Key</AlertDialog.Title>
            <AlertDialog.Description class="wrap-break-word">
                Are you sure you want to delete "<span class="break-all">{id}</span>" {#if notes}(<span class="break-all">{notes}</span>){/if}?
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>Delete API Key</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
