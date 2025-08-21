<script lang="ts">
    import { Notification, NotificationDescription, NotificationTitle } from '$comp/notification';

    interface Props {
        name: string;
        viewPlan?: () => void; // navigate to usage/plan view
    }

    let { name, viewPlan }: Props = $props();
</script>

<Notification variant="destructive">
    <NotificationTitle>Events are currently being throttled for {name}</NotificationTitle>
    <NotificationDescription>
        Events are currently being throttled to prevent using up your plan limit in a small window of time.
        {#if viewPlan}
            <button onclick={viewPlan}>Upgrade now</button>
        {:else}
            Upgrade now
        {/if}
        to increase your limits.
    </NotificationDescription>
</Notification>
