<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';

    interface Props {
        changePlan?: () => void; // navigate to plan change / billing
        name: string;
    }

    let { changePlan, name }: Props = $props();
</script>

<Notification variant="destructive">
    <NotificationTitle>{name} has reached its monthly plan limit.</NotificationTitle>
    <NotificationDescription>
        {#if changePlan}
            <button onclick={changePlan}>Upgrade now</button>
        {:else}
            Upgrade now
        {/if}
        to continue receiving events.
    </NotificationDescription>
</Notification>
