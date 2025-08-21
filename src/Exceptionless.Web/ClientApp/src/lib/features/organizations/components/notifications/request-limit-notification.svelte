<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { env } from '$env/dynamic/public';

    interface Props {
        name: string;
    }

    let { name }: Props = $props();

    const isIntercomEnabled = !!env.PUBLIC_INTERCOM_APPID;
</script>

<Notification variant="destructive">
    <NotificationTitle>API requests are currently being throttled for {name}</NotificationTitle>
    {#if isIntercomEnabled}
        <NotificationDescription>Please contact us for more information.</NotificationDescription>
    {/if}
</Notification>
