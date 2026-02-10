<script lang="ts">
    import type { NotificationProps } from '$comp/notification';

    import { resolve } from '$app/paths';
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';

    interface Props extends NotificationProps {
        name: string;
        organizationId: string;
        premiumFeatureName?: string;
    }

    let { name, organizationId, premiumFeatureName = "search", ...restProps }: Props = $props();
</script>

<Notification variant="information" {...restProps}>
    <NotificationTitle>{name} is attempting to use a premium feature.</NotificationTitle>
    <NotificationDescription>
        <A href={`${resolve('/(app)/organization/[organizationId]/billing', { organizationId })}?changePlan=true`}>Upgrade now</A>
        to enable {premiumFeatureName} and other premium features!
    </NotificationDescription>
</Notification>
