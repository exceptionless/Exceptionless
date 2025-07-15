<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        leave: () => Promise<void>;
        name: string;
        open: boolean;
    }

    let { leave, name, open = $bindable(false) }: Props = $props();

    async function onSubmit() {
        await leave();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Leave Organization</AlertDialog.Title>
            <AlertDialog.Description>
                Are you sure you want to leave "<span class="inline-block max-w-[200px] truncate align-bottom" title={name}>{name}</span>"?
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>Leave Organization</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
