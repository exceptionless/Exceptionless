<script lang="ts">
    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';

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
