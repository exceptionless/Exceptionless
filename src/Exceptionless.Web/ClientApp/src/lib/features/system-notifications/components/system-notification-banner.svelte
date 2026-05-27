<script lang="ts">
    import type { ReleaseNotification, SystemNotification } from '$features/websockets/models';

    import { env } from '$env/dynamic/public';
    import { Notification, NotificationDescription } from '$features/shared/components/notification';
    import { getCurrentSystemNotificationQuery } from '$features/system-notifications/api.svelte';
    import { resolveDisplayMessage } from '$features/system-notifications/resolve-message';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Info from '@lucide/svelte/icons/info';

    const currentNotificationQuery = getCurrentSystemNotificationQuery();

    let realtimeSystemMessage = $state<null | string | undefined>(undefined);
    let releaseMessage = $state<null | string>(null);

    const fallbackMessage = $derived(env.PUBLIC_SYSTEM_NOTIFICATION_MESSAGE || null);
    const persistedMessage = $derived(currentNotificationQuery.data?.message || null);
    const displaySystemMessage = $derived(resolveDisplayMessage(realtimeSystemMessage, persistedMessage, fallbackMessage));

    $effect(() => {
        function onSystemNotification(e: Event) {
            const detail = (e as CustomEvent<SystemNotification>).detail;
            realtimeSystemMessage = detail?.message || null;
        }

        function onReleaseNotification(e: Event) {
            const detail = (e as CustomEvent<ReleaseNotification>).detail;
            if (detail?.critical) {
                window.location.reload();
                return;
            }

            releaseMessage = detail?.message || null;
        }

        document.addEventListener('SystemNotification', onSystemNotification);
        document.addEventListener('ReleaseNotification', onReleaseNotification);

        return () => {
            document.removeEventListener('SystemNotification', onSystemNotification);
            document.removeEventListener('ReleaseNotification', onReleaseNotification);
        };
    });
</script>

{#if displaySystemMessage}
    <Notification variant="destructive" role="alert" aria-live="assertive" class="mb-4">
        {#snippet icon()}
            <AlertTriangle class="size-4" />
        {/snippet}
        <NotificationDescription>{displaySystemMessage}</NotificationDescription>
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
