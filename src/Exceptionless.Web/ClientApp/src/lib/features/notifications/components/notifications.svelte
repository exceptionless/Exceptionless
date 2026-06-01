<script lang="ts">
    import type { ReleaseNotification, SystemNotification } from '$features/websockets/models';

    // System notification messages are admin-authored HTML and sanitized before rendering.
    /* eslint svelte/no-at-html-tags: "off" */
    import { Button } from '$comp/ui/button';
    import { env } from '$env/dynamic/public';
    import { getCurrentSystemNotificationQuery } from '$features/notifications/api.svelte';
    import { Notification, NotificationDescription } from '$features/shared/components/notification';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Info from '@lucide/svelte/icons/info';
    import X from '@lucide/svelte/icons/x';
    import XCircle from '@lucide/svelte/icons/x-circle';
    import DOMPurify from 'dompurify';
    import { useEventListener } from 'runed';

    const currentNotificationQuery = getCurrentSystemNotificationQuery();
    const fallbackMessage = env.PUBLIC_SYSTEM_NOTIFICATION_MESSAGE || null;
    const dismissedSystemNotificationStorageKey = 'exceptionless.system-notification.dismissed-key';
    const purify = DOMPurify(window);

    type SystemNotificationTarget = 'Both' | 'Legacy' | 'Modern';

    function normalizeSystemNotificationTarget(target: null | string | undefined): SystemNotificationTarget {
        const normalizedTarget = (target ?? 'Both').replace(/[^a-z]/gi, '').toLowerCase();

        if (
            normalizedTarget === 'legacy' ||
            normalizedTarget === 'legacyui' ||
            normalizedTarget === 'oldui' ||
            normalizedTarget === 'old' ||
            normalizedTarget === 'angular'
        ) {
            return 'Legacy';
        }

        if (
            normalizedTarget === 'modern' ||
            normalizedTarget === 'modernui' ||
            normalizedTarget === 'newui' ||
            normalizedTarget === 'new' ||
            normalizedTarget === 'svelte'
        ) {
            return 'Modern';
        }

        return 'Both';
    }

    function getDismissedSystemNotificationKey() {
        if (typeof localStorage === 'undefined') {
            return null;
        }

        try {
            return localStorage.getItem(dismissedSystemNotificationStorageKey);
        } catch {
            return null;
        }
    }

    function getSystemNotificationDateKey(date: null | string | undefined) {
        if (!date) {
            return null;
        }

        const parsedDate = new Date(date);
        return Number.isNaN(parsedDate.getTime()) ? date : parsedDate.toISOString();
    }

    function getSystemNotificationKey(date: null | string | undefined, level: 'Error' | 'Info' | 'Warning', target: SystemNotificationTarget, message: string) {
        return JSON.stringify([getSystemNotificationDateKey(date), level, target, message]);
    }

    function setDismissedSystemNotificationKey(key: null | string) {
        dismissedSystemNotificationKey = key;

        if (!key || typeof localStorage === 'undefined') {
            return;
        }

        try {
            localStorage.setItem(dismissedSystemNotificationStorageKey, key);
        } catch {
            // Ignore storage failures; dismissal still works for the current page lifetime.
        }
    }

    let systemMessage = $state<null | string>(null);
    let systemLevel = $state<'Error' | 'Info' | 'Warning'>('Info');
    let systemTarget = $state<SystemNotificationTarget>('Both');
    let systemDate = $state<null | string>(null);
    let hasRealtimeSystemNotification = $state(false);
    let releaseMessage = $state<null | string>(null);
    let dismissedSystemNotificationKey = $state<null | string>(getDismissedSystemNotificationKey());
    let releaseDismissed = $state(false);

    const queryTarget = $derived<SystemNotificationTarget>(normalizeSystemNotificationTarget(currentNotificationQuery.data?.target));
    const effectiveTarget = $derived(hasRealtimeSystemNotification ? systemTarget : queryTarget);
    const showForModern = $derived(effectiveTarget === 'Both' || effectiveTarget === 'Modern');

    const displayMessage = $derived(
        hasRealtimeSystemNotification ? systemMessage || fallbackMessage : currentNotificationQuery.data?.message || fallbackMessage
    );
    const displayHtml = $derived(displayMessage ? purify.sanitize(displayMessage) : '');
    const displayLevel = $derived<'Error' | 'Info' | 'Warning'>(hasRealtimeSystemNotification ? systemLevel : (currentNotificationQuery.data?.level ?? 'Info'));
    const displayDate = $derived(hasRealtimeSystemNotification ? systemDate : (currentNotificationQuery.data?.date ?? null));
    const systemNotificationKey = $derived(displayMessage ? getSystemNotificationKey(displayDate, displayLevel, effectiveTarget, displayMessage) : null);

    const levelVariantMap = {
        Error: 'destructive',
        Info: 'information',
        Warning: 'warning'
    } as const;

    const levelIconMap = {
        Error: XCircle,
        Info: Info,
        Warning: AlertTriangle
    } as const;

    useEventListener(document, 'SystemNotification', (event: Event) => {
        const detail = (event as CustomEvent<SystemNotification>).detail;
        systemMessage = detail?.message || null;
        systemLevel = detail?.level ?? 'Info';
        systemTarget = normalizeSystemNotificationTarget(detail?.target);
        systemDate = detail?.date ?? null;
        hasRealtimeSystemNotification = true;
    });

    useEventListener(document, 'ReleaseNotification', (event: Event) => {
        const detail = (event as CustomEvent<ReleaseNotification>).detail;
        if (detail?.critical) {
            window.location.reload();
            return;
        }

        releaseMessage = detail?.message || null;
        releaseDismissed = false;
    });
</script>

{#if displayMessage && showForModern && systemNotificationKey !== dismissedSystemNotificationKey}
    {@const LevelIcon = levelIconMap[displayLevel]}
    <Notification variant={levelVariantMap[displayLevel]} role="alert" aria-live="assertive" class="mb-4">
        {#snippet icon()}
            <LevelIcon class="size-4" />
        {/snippet}
        {#snippet action()}
            <Button
                type="button"
                variant="ghost"
                size="icon"
                class="size-5 rounded-sm p-0 opacity-70 hover:opacity-100"
                onclick={() => setDismissedSystemNotificationKey(systemNotificationKey)}
                aria-label="Dismiss alert"
                title="Dismiss alert"
            >
                <X class="size-4" aria-hidden="true" />
            </Button>
        {/snippet}
        <NotificationDescription>{@html displayHtml}</NotificationDescription>
    </Notification>
{/if}

{#if releaseMessage && !releaseDismissed}
    <Notification variant="information" role="status" aria-live="polite" class="mb-4">
        {#snippet icon()}
            <Info class="size-4" />
        {/snippet}
        {#snippet action()}
            <Button
                type="button"
                variant="ghost"
                size="icon"
                class="size-5 rounded-sm p-0 opacity-70 hover:opacity-100"
                onclick={() => (releaseDismissed = true)}
                aria-label="Dismiss alert"
                title="Dismiss alert"
            >
                <X class="size-4" aria-hidden="true" />
            </Button>
        {/snippet}
        <NotificationDescription>{releaseMessage}</NotificationDescription>
    </Notification>
{/if}
