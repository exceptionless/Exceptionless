<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        open: boolean;
        remove: () => Promise<void>;
    }

    let { open = $bindable(), remove }: Props = $props();

    async function onSubmit() {
        await remove();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Remove Slack Integration</AlertDialog.Title>
            <AlertDialog.Description>
                Are you sure you want to remove the Slack integration from this project? This will disable all Slack notifications for this project.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>Remove Slack</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
