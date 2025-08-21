<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';
    import { env } from '$env/dynamic/public';
    import { SuspensionCode } from '$features/organizations/models';

    interface Props {
        isBilling?: boolean;
        manageBilling?: () => void; // navigate to billing management
        name: string;
        suspensionCode?: SuspensionCode;
    }

    let { isBilling = false, manageBilling, name, suspensionCode }: Props = $props();

    const isIntercomEnabled = !!env.PUBLIC_INTERCOM_APPID;
</script>

<Notification variant="destructive">
    <NotificationTitle>{name} has been suspended.</NotificationTitle>
    <NotificationDescription>
        <em>Please note that while your account is suspended all new client events will be discarded.</em>

        {#if isBilling}
            <p>
                To unsuspend <strong>{name}</strong>, please
                {#if manageBilling}
                    <button onclick={manageBilling}>update your billing information</button>.
                {:else}
                    update your billing information.
                {/if}
            </p>
        {:else if suspensionCode === SuspensionCode.Abuse || suspensionCode === SuspensionCode.Overage}
            <p>
                <strong>{name}</strong> has exceeded the plan limits. To unsuspend your account, please
                {#if manageBilling}
                    <button onclick={manageBilling}>upgrade your plan</button>.
                {:else}
                    upgrade your plan.
                {/if}
            </p>
        {/if}

        {#if isIntercomEnabled}
            <p>Please contact us for more information on why your account was suspended.</p>
        {/if}
    </NotificationDescription>
</Notification>
