<script lang="ts">
    import Number from '$comp/formatters/number.svelte';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Button } from '$comp/ui/button';
    import { getProblemMessage } from '$shared/validation';
    import { toast } from 'svelte-sonner';

    interface Props {
        count?: number;
        discard: () => Promise<void>;
        open: boolean;
        stackTitle?: string;
    }

    let { count = 1, discard, open = $bindable(), stackTitle }: Props = $props();
    let submitting = $state(false);

    async function onSubmit() {
        if (submitting) {
            return;
        }
        submitting = true;
        try {
            await discard();
            open = false;
        } catch (error) {
            toast.error(getProblemMessage(error, 'Unable to discard the selected stacks.'));
        } finally {
            submitting = false;
        }
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>
                Discard
                {#if count === 1}
                    Stack
                {:else}
                    <Number value={count} /> Stacks
                {/if}
            </AlertDialog.Title>
            <AlertDialog.Description>
                {#if stackTitle}<strong>{stackTitle}</strong><br />{/if}
                Are you sure you want to discard all current
                {#if count === 1}
                    stack events
                {:else}
                    <Number value={count} /> stacks events
                {/if}
                and discard any future events?
            </AlertDialog.Description>
        </AlertDialog.Header>
        All future occurrences will be discarded and will not count against your event limit.
        <AlertDialog.Footer>
            <AlertDialog.Cancel disabled={submitting}>Cancel</AlertDialog.Cancel>
            <Button variant="destructive" disabled={submitting} onclick={onSubmit}>
                Discard
                {#if count === 1}
                    Stack
                {:else}
                    <Number value={count} /> Stacks
                {/if}
            </Button>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
