<script lang="ts">
    // System notification messages are admin-authored HTML and sanitized before rendering.
    /* eslint svelte/no-at-html-tags: "off" */
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Label } from '$comp/ui/label';
    import * as Select from '$comp/ui/select';
    import { Textarea } from '$comp/ui/textarea';
    import { clearSystemNotificationMutation, getCurrentSystemNotificationQuery, setSystemNotificationMutation } from '$features/notifications/api.svelte';
    import { Notification, NotificationDescription } from '$features/shared/components/notification';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Info from '@lucide/svelte/icons/info';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import X from '@lucide/svelte/icons/x';
    import XCircle from '@lucide/svelte/icons/x-circle';
    import DOMPurify from 'dompurify';
    import { toast } from 'svelte-sonner';

    const currentNotificationQuery = getCurrentSystemNotificationQuery();
    const setSystemNotification = setSystemNotificationMutation();
    const clearSystemNotification = clearSystemNotificationMutation();
    const purify = DOMPurify(window);

    let systemMessage = $state('');
    let systemLevel = $state<'Error' | 'Info' | 'Warning'>('Info');
    let systemTarget = $state<'Both' | 'Legacy' | 'Modern'>('Both');
    let loadedNotificationKey = $state<null | string>(null);

    const currentNotification = $derived(currentNotificationQuery.data);
    const hasCurrentNotification = $derived(!!currentNotification?.message);
    const hasPreviewNotification = $derived(!!systemMessage.trim());
    const previewNotificationHtml = $derived(systemMessage.trim() ? purify.sanitize(systemMessage) : '');
    const currentNotificationKey = $derived(
        currentNotification
            ? JSON.stringify([
                  currentNotification.date,
                  currentNotification.level ?? 'Info',
                  currentNotification.target ?? 'Both',
                  currentNotification.message ?? ''
              ])
            : null
    );

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

    const targetLabelMap = {
        Both: 'Both UIs',
        Legacy: 'Legacy UI only',
        Modern: 'New UI only'
    } as const;

    $effect(() => {
        if (loadedNotificationKey === currentNotificationKey) {
            return;
        }

        loadedNotificationKey = currentNotificationKey;

        if (currentNotification?.message) {
            systemMessage = currentNotification.message;
            systemLevel = currentNotification.level ?? 'Info';
            systemTarget = currentNotification.target ?? 'Both';
            return;
        }

        if (!currentNotificationQuery.isLoading) {
            systemMessage = '';
            systemLevel = 'Info';
            systemTarget = 'Both';
        }
    });

    async function handleSetSystemNotification() {
        try {
            await setSystemNotification.mutateAsync({ level: systemLevel, message: systemMessage, target: systemTarget });
            toast.success('System notification set successfully.');
        } catch {
            toast.error('Failed to set system notification.');
        }
    }

    async function handleClearSystemNotification() {
        try {
            await clearSystemNotification.mutateAsync();
            toast.success('System notification cleared.');
            systemMessage = '';
            systemLevel = 'Info';
            systemTarget = 'Both';
        } catch {
            toast.error('Failed to clear system notification.');
        }
    }
</script>

<div class="space-y-6">
    <Muted>Manage system notification banners</Muted>

    <Card.Root class="bg-transparent">
        <Card.Content class="space-y-6">
            <div class="space-y-2">
                <div class="text-sm font-medium">Current Notification</div>
                {#if currentNotificationQuery.isLoading}
                    <Muted>Loading...</Muted>
                {:else if hasPreviewNotification}
                    {@const LevelIcon = levelIconMap[systemLevel]}
                    <div class="space-y-2">
                        <Notification variant={levelVariantMap[systemLevel]} role="status" aria-live="polite">
                            {#snippet icon()}
                                <LevelIcon class="size-4" />
                            {/snippet}
                            {#snippet action()}
                                <Button
                                    type="button"
                                    variant="ghost"
                                    size="icon"
                                    class="size-5 rounded-sm p-0 opacity-70 hover:opacity-100"
                                    onclick={(event) => {
                                        event.preventDefault();
                                    }}
                                    aria-label="Dismiss alert"
                                    title="Dismiss alert"
                                >
                                    <X class="size-4" aria-hidden="true" />
                                </Button>
                            {/snippet}
                            <NotificationDescription>{@html previewNotificationHtml}</NotificationDescription>
                        </Notification>
                        <Muted>
                            Level: {systemLevel} &middot; Target: {targetLabelMap[systemTarget]}
                            {#if currentNotification?.date}
                                &middot; Set {new Date(currentNotification.date).toLocaleString()}
                            {/if}
                        </Muted>
                    </div>
                {:else}
                    <Muted>(no active notification)</Muted>
                {/if}
            </div>

            <div class="space-y-2">
                <Label for="system-message">Message</Label>
                <Textarea id="system-message" bind:value={systemMessage} placeholder="Enter notification message..." rows={4} />
                <Muted>HTML is supported and will be sanitized before display.</Muted>
            </div>

            <div class="grid gap-6 lg:grid-cols-2">
                <div class="space-y-2">
                    <Label>Level</Label>
                    <Select.Root type="single" bind:value={systemLevel}>
                        <Select.Trigger class="w-40">
                            {systemLevel}
                        </Select.Trigger>
                        <Select.Content>
                            <Select.Item value="Info">Info</Select.Item>
                            <Select.Item value="Warning">Warning</Select.Item>
                            <Select.Item value="Error">Error</Select.Item>
                        </Select.Content>
                    </Select.Root>
                </div>

                <div class="space-y-2">
                    <Label>Target</Label>
                    <Select.Root type="single" bind:value={systemTarget}>
                        <Select.Trigger class="w-40">
                            {targetLabelMap[systemTarget]}
                        </Select.Trigger>
                        <Select.Content>
                            <Select.Item value="Both">Both UIs</Select.Item>
                            <Select.Item value="Legacy">Legacy UI only</Select.Item>
                            <Select.Item value="Modern">New UI only</Select.Item>
                        </Select.Content>
                    </Select.Root>
                </div>
            </div>
        </Card.Content>
        <Card.Footer class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            {#if hasCurrentNotification}
                <Button variant="outline" onclick={handleClearSystemNotification} disabled={clearSystemNotification.isPending}>
                    <Trash2 class="size-4" />
                    {clearSystemNotification.isPending ? 'Clearing...' : 'Clear Notification'}
                </Button>
            {/if}
            <Button onclick={handleSetSystemNotification} disabled={!systemMessage.trim() || setSystemNotification.isPending}>
                {setSystemNotification.isPending ? 'Saving...' : hasCurrentNotification ? 'Update Notification' : 'Set Notification'}
            </Button>
        </Card.Footer>
    </Card.Root>
</div>
