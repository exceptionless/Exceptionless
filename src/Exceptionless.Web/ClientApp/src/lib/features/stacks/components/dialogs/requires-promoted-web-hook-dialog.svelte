<script lang="ts">
    import * as AlertDialog from '$comp/ui/alert-dialog';
    interface Props {
        navigate: () => Promise<void>;
        open: boolean;
    }

    let { navigate, open = $bindable() }: Props = $props();

    async function onSubmit() {
        await navigate();
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Manage Integrations</AlertDialog.Title>
            <AlertDialog.Description
                >No promoted web hooks are configured for this project. Please add a promoted web hook to use this feature.</AlertDialog.Description
            >
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action onclick={onSubmit}>Manage Integrations</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
