<script lang="ts">
    import type { NotificationProps } from '$comp/notification';

    import { resolve } from '$app/paths';
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props extends NotificationProps {
        name: string;
        organizationId: string;
    }

    let { name, organizationId, ...restProps }: Props = $props();
</script>

<Notification variant="destructive" {...restProps}>
    <NotificationTitle>{name} has reached its monthly plan limit.</NotificationTitle>
    <NotificationDescription>
        <A href={`${resolve('/(app)/organization/[organizationId]/billing', { organizationId })}?changePlan=true`}>Upgrade now</A>
        to continue receiving events.
    </NotificationDescription>
</Notification>
