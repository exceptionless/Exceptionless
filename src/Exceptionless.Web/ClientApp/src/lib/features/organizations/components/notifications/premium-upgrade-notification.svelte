<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';

    interface Props {
        name: string;
        upgradePlan?: () => void; // navigate to upgrade path
    }

    let { name, upgradePlan }: Props = $props();
</script>

<Notification variant="information">
    <NotificationTitle>{name} is attempting to use a premium feature.</NotificationTitle>
    <NotificationDescription>
        {#if upgradePlan}
            <button onclick={upgradePlan}>Upgrade now</button>
        {:else}
            Upgrade now
        {/if}
        to enable search and other premium features!
    </NotificationDescription>
</Notification>
