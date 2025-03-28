<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';

    interface Props {
        enable: () => Promise<void>;
        id: string;
        notes?: null | string;
        open: boolean;
    }

    let { enable, id, notes, open = $bindable(false) }: Props = $props();

    async function onSubmit() {
        await enable();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Enable API Key</AlertDialog.Title>
            <AlertDialog.Description>
                Are you sure you want to enable "{id}" {#if notes}({notes}){/if}?
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action onclick={onSubmit}>Enable API Key</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
