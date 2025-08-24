<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props {
        name: string;
        organizationId: string;
    }

    let { name, organizationId }: Props = $props();
</script>

<Notification variant="information">
    <NotificationTitle>{name} is attempting to use a premium feature.</NotificationTitle>
    <NotificationDescription>
        <A href={`/next/organization/${organizationId}/billing?changePlan=true`}>Upgrade now</A>
        to enable search and other premium features!
    </NotificationDescription>
</Notification>
