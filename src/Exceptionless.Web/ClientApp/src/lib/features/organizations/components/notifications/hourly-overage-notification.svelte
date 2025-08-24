<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props {
        name: string;
        organizationId: string;
    }

    let { name, organizationId }: Props = $props();
</script>

<Notification variant="destructive">
    <NotificationTitle>Events are currently being throttled for {name}</NotificationTitle>
    <NotificationDescription>
        Events are currently being throttled to prevent using up your plan limit in a small window of time.
        <A href={`/next/organization/${organizationId}/billing?changePlan=true`}>Upgrade now</A> to increase your limits.
    </NotificationDescription>
</Notification>
