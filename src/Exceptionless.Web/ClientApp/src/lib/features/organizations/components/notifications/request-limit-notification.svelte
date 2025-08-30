<script lang="ts">
    import type { NotificationProps } from '$comp/notification';

    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props extends NotificationProps {
        isChatEnabled: boolean;
        name: string;
        openChat: () => void;
    }

    let { isChatEnabled, name, openChat, ...restProps }: Props = $props();
</script>

<Notification variant="destructive" {...restProps}>
    <NotificationTitle>API requests are currently being throttled for {name}</NotificationTitle>
    {#if isChatEnabled}
        <NotificationDescription>
            Please <A onclick={openChat}>contact support</A> for more information.
        </NotificationDescription>
    {/if}
</Notification>
