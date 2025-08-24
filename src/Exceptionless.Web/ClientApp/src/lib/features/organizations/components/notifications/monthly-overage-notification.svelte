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
    <NotificationTitle>{name} has reached its monthly plan limit.</NotificationTitle>
    <NotificationDescription>
        <A href={`/next/organization/${organizationId}/billing?changePlan=true`}>Upgrade now</A>
        to continue receiving events.
    </NotificationDescription>
</Notification>
