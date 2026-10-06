<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button } from '$comp/ui/button';
    import { getProblemMessage } from '$shared/validation';
    import { toast } from 'svelte-sonner';

    interface Props {
        ignore: () => Promise<void>;
        open: boolean;
        stackTitle: string;
    }

    let { ignore, open = $bindable(), stackTitle }: Props = $props();
    let submitting = $state(false);

    async function onSubmit() {
        if (submitting) {
            return;
        }
        submitting = true;
        try {
            await ignore();
            open = false;
        } catch (error) {
            toast.error(getProblemMessage(error, 'Unable to ignore this stack.'));
        } finally {
            submitting = false;
        }
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Ignore Stack</AlertDialog.Title>
            <AlertDialog.Description>
                Stop sending occurrence notifications for <strong>{stackTitle}</strong>? Future events will still be collected.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel disabled={submitting}>Cancel</AlertDialog.Cancel>
            <Button disabled={submitting} onclick={onSubmit}>Ignore Stack</Button>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
