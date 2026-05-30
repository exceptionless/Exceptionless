<script lang="ts">
    import type { ReleaseNotification, SystemNotification } from '$features/websockets/models';

    import { env } from '$env/dynamic/public';
    import { getCurrentSystemNotificationQuery } from '$features/notifications/api.svelte';
    import { Notification, NotificationDescription } from '$features/shared/components/notification';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Info from '@lucide/svelte/icons/info';
    import { useEventListener } from 'runed';

    const currentNotificationQuery = getCurrentSystemNotificationQuery();
    const fallbackMessage = env.PUBLIC_SYSTEM_NOTIFICATION_MESSAGE || null;

    let systemMessage = $state<null | string>(null);
    let releaseMessage = $state<null | string>(null);

    const displayMessage = $derived(systemMessage || currentNotificationQuery.data?.message || fallbackMessage);

    useEventListener(document, 'SystemNotification', (event: Event) => {
        const detail = (event as CustomEvent<SystemNotification>).detail;
        systemMessage = detail?.message || null;
    });

    useEventListener(document, 'ReleaseNotification', (event: Event) => {
        const detail = (event as CustomEvent<ReleaseNotification>).detail;
        if (detail?.critical) {
            window.location.reload();
            return;
        }

        releaseMessage = detail?.message || null;
    });
</script>

{#if displayMessage}
    <Notification variant="destructive" role="alert" aria-live="assertive" class="mb-4">
        {#snippet icon()}
            <AlertTriangle class="size-4" />
        {/snippet}
        <NotificationDescription>{displayMessage}</NotificationDescription>
    </Notification>
{/if}

{#if releaseMessage}
    <Notification variant="information" role="status" aria-live="polite" class="mb-4">
        {#snippet icon()}
            <Info class="size-4" />
        {/snippet}
        <NotificationDescription>{releaseMessage}</NotificationDescription>
    </Notification>
{/if}
