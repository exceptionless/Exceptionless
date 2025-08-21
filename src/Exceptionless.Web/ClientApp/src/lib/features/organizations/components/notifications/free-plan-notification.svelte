<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';

    interface Props {
        changePlan?: () => void; // navigate to billing/plan change
        name: string;
    }

    let { changePlan, name }: Props = $props();
</script>

<Notification variant="information">
    <NotificationTitle>{name} is currently on a free plan.</NotificationTitle>
    <NotificationDescription>
        {#if changePlan}
            <button onclick={changePlan}>Upgrade now</button>
        {:else}
            Upgrade now
        {/if}
        to enable premium features and extra storage!
    </NotificationDescription>
</Notification>
