<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        discard: () => Promise<void>;
        open: boolean;
    }

    let { discard, open = $bindable() }: Props = $props();

    async function onSubmit() {
        await discard();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Discard Stack</AlertDialog.Title>
            <AlertDialog.Description>Are you sure you want to all current stack events and discard any future stack events?</AlertDialog.Description>
        </AlertDialog.Header>
        All future occurrences will be discarded and will not count against your event limit.
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>Discard Stack</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
