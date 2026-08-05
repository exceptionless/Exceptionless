<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        disable: () => Promise<void>;
        id: string;
        notes?: null | string;
        open: boolean;
    }

    let { disable, id, notes, open = $bindable(false) }: Props = $props();

    async function onSubmit() {
        await disable();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Disable Token</AlertDialog.Title>
            <AlertDialog.Description>
                Are you sure you want to disable "{id}" {#if notes}({notes}){/if}?
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>Disable Token</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
