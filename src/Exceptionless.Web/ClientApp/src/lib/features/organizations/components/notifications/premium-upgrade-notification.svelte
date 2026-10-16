<script lang="ts">
    import type { NotificationProps } from '#comp/notification/index.ts';

    import { Notification, NotificationDescription, NotificationTitle } from '#comp/notification/index.ts';
    import { A } from '#comp/typography/index.ts';
    import { showUpgradeDialog } from '#features/billing/upgrade-required.svelte.ts';

    interface Props extends NotificationProps {
        name: string;
        organizationId: string;
        premiumFeatureName?: string;
    }

    let { name, organizationId, premiumFeatureName = 'search', ...restProps }: Props = $props();
</script>

<Notification variant="information" {...restProps}>
    <NotificationTitle>{name} is attempting to use a premium feature.</NotificationTitle>
    <NotificationDescription>
        <A onclick={() => showUpgradeDialog(organizationId, `Upgrade to enable ${premiumFeatureName} and other premium features.`)}>Upgrade now</A>
        to enable {premiumFeatureName} and other premium features!
    </NotificationDescription>
</Notification>
