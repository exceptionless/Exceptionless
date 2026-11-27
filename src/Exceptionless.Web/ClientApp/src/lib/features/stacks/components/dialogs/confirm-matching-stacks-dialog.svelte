<script lang="ts">
    import { toast } from 'svelte-sonner';

    import Number from '#comp/formatters/number.svelte';
    import * as AlertDialog from '#comp/ui/alert-dialog/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import { getProblemMessage } from '#shared/validation.ts';

    interface Props {
        confirm: () => Promise<void>;
        /** The estimated number of matching stacks. */
        count: number;
        /** What the chosen option does, e.g. how long a snooze lasts. */
        detail?: string;
        /** What happens, e.g. "Mark Matching Stacks Ignored". Used as the title and the button label. */
        label: string;
        open: boolean;
    }

    let { confirm, count, detail, label, open = $bindable() }: Props = $props();
    let submitting = $state(false);

    async function onSubmit() {
        if (submitting) {
            return;
        }

        submitting = true;
        try {
            await confirm();
            open = false;
        } catch (error) {
            toast.error(getProblemMessage(error, 'Unable to start the operation.'));
        } finally {
            submitting = false;
        }
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>{label}</AlertDialog.Title>
            <AlertDialog.Description>
                About <Number value={count} />
                {count === 1 ? 'stack matches' : 'stacks match'} this filter. The final count may differ.
                {#if detail}
                    {detail}
                {/if}
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel disabled={submitting}>Cancel</AlertDialog.Cancel>
            <Button disabled={submitting} onclick={onSubmit}>{label}</Button>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
