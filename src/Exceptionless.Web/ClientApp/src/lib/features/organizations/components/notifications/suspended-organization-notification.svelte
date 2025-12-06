<script lang="ts">
    import type { NotificationProps } from '$comp/notification';

    import { resolve } from '$app/paths';
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { A } from '$comp/typography';
    import { SuspensionCode } from '$features/organizations/models';

    interface Props extends NotificationProps {
        isChatEnabled: boolean;
        name: string;
        openChat: () => void;
        organizationId: string;
        suspensionCode?: SuspensionCode;
    }

    let { isChatEnabled, name, openChat, organizationId, suspensionCode, ...restProps }: Props = $props();

    const changePlanHref = $derived(resolve('/(app)/organization/[organizationId]/billing', { organizationId }) + '?changePlan=true');
</script>

<Notification variant="destructive" {...restProps}>
    <NotificationTitle>{name} has been suspended.</NotificationTitle>
    <NotificationDescription>
        <em>Please note that while your account is suspended all new client events will be discarded.</em>

        {#if suspensionCode === SuspensionCode.Billing}
            <p>
                To unsuspend <strong>{name}</strong>, please
                <A href={changePlanHref}>update your billing information</A>.
            </p>
        {:else if suspensionCode === SuspensionCode.Abuse || suspensionCode === SuspensionCode.Overage}
            <p>
                <strong>{name}</strong> has exceeded the plan limits. To unsuspend your account, please
                <A href={changePlanHref}>upgrade your plan</A>.
            </p>
        {/if}

        {#if isChatEnabled}
            <p>
                Please <A onclick={openChat}>contact support</A> for more information on why your account was suspended.
            </p>
        {/if}
    </NotificationDescription>
</Notification>
