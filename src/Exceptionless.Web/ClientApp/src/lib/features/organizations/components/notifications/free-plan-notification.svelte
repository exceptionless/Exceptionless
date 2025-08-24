<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props {
        name: string;
        organizationId: string;
    }

    let { name, organizationId }: Props = $props();
</script>

<Notification variant="success">
    <NotificationTitle>{name} is currently on a free plan.</NotificationTitle>
    <NotificationDescription>
        <A href={`/next/organization/${organizationId}/billing?changePlan=true`}>Upgrade now</A>
        to enable premium features and extra storage!
    </NotificationDescription>
</Notification>
