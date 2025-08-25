<script lang="ts">
    import { P } from '$comp/typography';
    import * as AlertDialog from '$comp/ui/alert-dialog';

    interface Props {
        open: boolean;
    }

    let { open = $bindable() }: Props = $props();

    function onOk() {
        open = false;
    }
</script>

<AlertDialog.Root bind:open>
    <AlertDialog.Content>
        <AlertDialog.Header>
            <AlertDialog.Title>Change Plan</AlertDialog.Title>
            <AlertDialog.Description>
                <P>We're still working on this feature in the new app. In the meantime, you can update your plan in our previous app.</P>
            </AlertDialog.Description>
        </AlertDialog.Header>
        <AlertDialog.Footer>
            <AlertDialog.Cancel>Close</AlertDialog.Cancel>
            <AlertDialog.Action onclick={onOk}>OK</AlertDialog.Action>
        </AlertDialog.Footer>
    </AlertDialog.Content>
</AlertDialog.Root>
