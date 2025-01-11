<script lang="ts">
    import Number from '$comp/formatters/number.svelte';
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        count?: number;
        open: boolean;
        remove: () => Promise<void>;
    }

    let { count = 1, open = $bindable(), remove }: Props = $props();

    async function onSubmit() {
        await remove();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>
                Delete
                {#if count === 1}
                    Stack
                {:else}
                    <Number value={count} /> Stacks
                {/if}
            </AlertDialog.Title>
            <AlertDialog.Description>
                Are you sure you want to delete
                {#if count === 1}
                    stack
                {:else}
                    <Number value={count} /> stacks
                {/if}
                (including all related events)?
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>
                Delete
                {#if count === 1}
                    Stack
                {:else}
                    <Number value={count} /> Stacks
                {/if}
            </AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
