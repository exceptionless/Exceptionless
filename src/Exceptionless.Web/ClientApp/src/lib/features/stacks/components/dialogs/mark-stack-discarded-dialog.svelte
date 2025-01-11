<script lang="ts">
    import Number from '$comp/formatters/number.svelte';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        count?: number;
        discard: () => Promise<void>;
        open: boolean;
    }

    let { count = 1, discard, open = $bindable() }: Props = $props();

    async function onSubmit() {
        await discard();
        open = false;
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
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>
                Discard
                {#if count === 1}
                    Stack
                {:else}
                    <Number value={count} /> Stacks
                {/if}
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
