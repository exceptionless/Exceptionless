<script lang="ts">
    import { Code } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        open: boolean;
        reference: string;
        remove: (reference: string) => Promise<void>;
    }

    let { open = $bindable(), reference, remove }: Props = $props();

    async function onSubmit() {
        await remove(reference);
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Delete Reference</AlertDialog.Title>
            <AlertDialog.Description>Are you sure you want to delete this reference link?</AlertDialog.Description>
        </AlertDialog.Header>
        <Code>{reference}</Code>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>Delete Reference Link</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
