<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props {
        isChatEnabled: boolean;
        name: string;
        openChat: () => void;
    }

    let { isChatEnabled, name, openChat }: Props = $props();
</script>

<Notification variant="destructive">
    <NotificationTitle>API requests are currently being throttled for {name}</NotificationTitle>
    {#if isChatEnabled}
        <NotificationDescription>
            Please <A onclick={openChat}>contact us</A> for more information.
        </NotificationDescription>
    {/if}
</Notification>
