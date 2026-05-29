<script lang="ts">
    import { H2, Muted, P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import { Checkbox } from '$comp/ui/checkbox';
    import * as Dialog from '$comp/ui/dialog';
    import { Label } from '$comp/ui/label';
    import { Textarea } from '$comp/ui/textarea';
    import {
        clearSystemNotificationMutation,
        getCurrentSystemNotificationQuery,
        sendReleaseNotificationMutation,
        setSystemNotificationMutation
    } from '$features/notifications/api.svelte';
    import AlertTriangle from '@lucide/svelte/icons/alert-triangle';
    import Bell from '@lucide/svelte/icons/bell';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import { toast } from 'svelte-sonner';

    const currentNotificationQuery = getCurrentSystemNotificationQuery();
    const setSystemNotification = setSystemNotificationMutation();
    const clearSystemNotification = clearSystemNotificationMutation();
    const sendRelease = sendReleaseNotificationMutation();

    // Dialog state
    let showSetDialog = $state(false);
    let showClearDialog = $state(false);
    let showReleaseDialog = $state(false);

    // Form state
    let systemMessage = $state('');
    let systemPublish = $state(true);
    let clearPublish = $state(true);
    let releaseMessage = $state('');
    let releaseCritical = $state(false);

    async function handleSetSystemNotification() {
        try {
            await setSystemNotification.mutateAsync({ message: systemMessage, publish: systemPublish });
            toast.success('System notification set successfully.');
            showSetDialog = false;
            systemMessage = '';
            systemPublish = true;
        } catch {
            toast.error('Failed to set system notification.');
        }
    }

    async function handleClearSystemNotification() {
        try {
            await clearSystemNotification.mutateAsync({ publish: clearPublish });
            toast.success('System notification cleared.');
            showClearDialog = false;
            clearPublish = true;
        } catch {
            toast.error('Failed to clear system notification.');
        }
    }

    async function handleSendReleaseNotification() {
        try {
            await sendRelease.mutateAsync({ critical: releaseCritical, message: releaseMessage || undefined });
            toast.success('Release notification sent.');
            showReleaseDialog = false;
            releaseMessage = '';
            releaseCritical = false;
        } catch {
            toast.error('Failed to send release notification.');
        }
    }

</script>

<div class="space-y-6">
    <div>
        <H2>Notifications</H2>
        <Muted>Manage system notifications and release announcements.</Muted>
    </div>

    <Card.Root>
        <Card.Header>
            <Card.Title>Current Status</Card.Title>
            <Card.Description>Active system notification state.</Card.Description>
        </Card.Header>
        <Card.Content>
            {#if currentNotificationQuery.isLoading}
                <Muted>Loading...</Muted>
            {:else if currentNotificationQuery.data?.message}
                <P class="text-red-600 dark:text-red-400">
                    {currentNotificationQuery.data.message}
                </P>
                <Muted>
                    Set {new Date(currentNotificationQuery.data.date).toLocaleString()}
                </Muted>
            {:else}
                <Muted>(no active notification)</Muted>
            {/if}
        </Card.Content>
    </Card.Root>

    <div class="grid gap-4 md:grid-cols-2">
        <Card.Root>
            <Card.Header>
                <Card.Title class="flex items-center gap-2">
                    <Bell class="size-4" />
                    Set System Notification
                </Card.Title>
                <Card.Description>Display a persistent notification banner to all users.</Card.Description>
            </Card.Header>
            <Card.Content>
                <Button onclick={() => (showSetDialog = true)} disabled={setSystemNotification.isPending}>Set Notification</Button>
            </Card.Content>
        </Card.Root>

        <Card.Root>
            <Card.Header>
                <Card.Title class="flex items-center gap-2">
                    <Trash2 class="size-4" />
                    Clear System Notification
                </Card.Title>
                <Card.Description>Remove the current system notification banner.</Card.Description>
            </Card.Header>
            <Card.Content>
                <Button variant="outline" onclick={() => (showClearDialog = true)} disabled={clearSystemNotification.isPending}>Clear Notification</Button>
            </Card.Content>
        </Card.Root>

        <Card.Root>
            <Card.Header>
                <Card.Title class="flex items-center gap-2">
                    <AlertTriangle class="size-4" />
                    Send Release Notification
                </Card.Title>
                <Card.Description>Send a one-time release announcement to all connected clients.</Card.Description>
            </Card.Header>
            <Card.Content>
                <Button variant="outline" onclick={() => (showReleaseDialog = true)} disabled={sendRelease.isPending}>Send Release Notification</Button>
            </Card.Content>
        </Card.Root>

    </div>
</div>

<!-- Set System Notification Dialog -->
<Dialog.Root bind:open={showSetDialog}>
    <Dialog.Content>
        <Dialog.Header>
            <Dialog.Title>Set System Notification</Dialog.Title>
            <Dialog.Description>This message will be displayed as a banner to all users.</Dialog.Description>
        </Dialog.Header>
        <div class="space-y-4 py-4">
            <div class="space-y-2">
                <Label for="system-message">Message</Label>
                <Textarea id="system-message" bind:value={systemMessage} placeholder="Enter notification message..." rows={3} />
            </div>
            <div class="flex items-center gap-2">
                <Checkbox id="system-publish" bind:checked={systemPublish} />
                <Label for="system-publish">Publish in realtime via WebSocket</Label>
            </div>
        </div>
        <Dialog.Footer>
            <Button variant="outline" onclick={() => (showSetDialog = false)}>Cancel</Button>
            <Button onclick={handleSetSystemNotification} disabled={!systemMessage.trim() || setSystemNotification.isPending}>
                {setSystemNotification.isPending ? 'Setting...' : 'Set Notification'}
            </Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>

<!-- Clear System Notification Dialog -->
<Dialog.Root bind:open={showClearDialog}>
    <Dialog.Content>
        <Dialog.Header>
            <Dialog.Title>Clear System Notification</Dialog.Title>
            <Dialog.Description>This will remove the current system notification for all users.</Dialog.Description>
        </Dialog.Header>
        <div class="space-y-4 py-4">
            <div class="flex items-center gap-2">
                <Checkbox id="clear-publish" bind:checked={clearPublish} />
                <Label for="clear-publish">Publish removal in realtime via WebSocket</Label>
            </div>
        </div>
        <Dialog.Footer>
            <Button variant="outline" onclick={() => (showClearDialog = false)}>Cancel</Button>
            <Button variant="destructive" onclick={handleClearSystemNotification} disabled={clearSystemNotification.isPending}>
                {clearSystemNotification.isPending ? 'Clearing...' : 'Clear Notification'}
            </Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>

<!-- Send Release Notification Dialog -->
<Dialog.Root bind:open={showReleaseDialog}>
    <Dialog.Content>
        <Dialog.Header>
            <Dialog.Title>Send Release Notification</Dialog.Title>
            <Dialog.Description>Send a one-time notification to all connected clients.</Dialog.Description>
        </Dialog.Header>
        <div class="space-y-4 py-4">
            <div class="space-y-2">
                <Label for="release-message">Message (optional)</Label>
                <Textarea id="release-message" bind:value={releaseMessage} placeholder="Enter release message..." rows={3} />
            </div>
            <div class="flex items-center gap-2">
                <Checkbox id="release-critical" bind:checked={releaseCritical} />
                <Label for="release-critical" class="text-destructive font-medium">Critical (forces client reload)</Label>
            </div>
        </div>
        <Dialog.Footer>
            <Button variant="outline" onclick={() => (showReleaseDialog = false)}>Cancel</Button>
            <Button onclick={handleSendReleaseNotification} disabled={sendRelease.isPending}>
                {sendRelease.isPending ? 'Sending...' : 'Send Notification'}
            </Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
