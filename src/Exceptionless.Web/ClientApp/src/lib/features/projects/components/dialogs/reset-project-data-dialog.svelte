<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        name: string;
        open: boolean;
        reset: () => Promise<void>;
    }

    let { name, open = $bindable(false), reset }: Props = $props();

    async function onSubmit() {
        await reset();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Reset Project Data</AlertDialog.Title>
            <AlertDialog.Description>
                Are you sure you want to reset all project data for "<span class="truncate max-w-[200px] inline-block align-bottom" title={name}>{name}</span>"? This action cannot be undone and will permanently erase all events, stacks, and
                associated data.
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>Reset Project Data</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
