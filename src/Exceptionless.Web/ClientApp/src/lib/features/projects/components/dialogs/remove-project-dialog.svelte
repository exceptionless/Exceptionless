<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { buttonVariants } from '$comp/ui/button';

    interface Props {
        name: string;
        open: boolean;
        remove: () => Promise<void>;
    }

    let { name, open = $bindable(false), remove }: Props = $props();

    async function onSubmit() {
        await remove();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Delete Project</AlertDialog.Title>
            <AlertDialog.Description>
                Are you sure you want to delete "<span class="truncate max-w-[200px] inline-block align-bottom" title={name}>{name}</span>"?
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action class={buttonVariants({ variant: 'destructive' })} onclick={onSubmit}>Delete Project</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
