<script module lang="ts">
    import { A } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import Ban from '@lucide/svelte/icons/ban';
    import CheckCircle from '@lucide/svelte/icons/check-circle';
    import Eye from '@lucide/svelte/icons/eye';
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import NotificationDescription from './notification-description.svelte';
    import NotificationTitle from './notification-title.svelte';
    import Notification from './notification.svelte';

    const { Story } = defineMeta({
        component: Notification,
        tags: ['autodocs'],
        title: 'Components/Shared/Notification'
    });
</script>

<Story name="Success">
    <Notification variant="success">
        {#snippet icon()}<CheckCircle />{/snippet}
        <NotificationTitle>Operation completed successfully!</NotificationTitle>
        <NotificationDescription>Your changes have been saved and are now active. All affected users will see the updates immediately.</NotificationDescription>
    </Notification>
</Story>

<Story name="Destructive">
    <Notification variant="destructive">
        {#snippet icon()}<Ban />{/snippet}
        <NotificationTitle>Something went wrong</NotificationTitle>
        <NotificationDescription>
            An error occurred while processing your request. Please try again or <A onclick={() => {}}>contact support</A> if the issue persists.
        </NotificationDescription>
    </Notification>
</Story>

<Story name="Warning">
    <Notification variant="warning">
        <NotificationTitle>Action required</NotificationTitle>
        <NotificationDescription>You're approaching your usage limit. Consider upgrading your plan to avoid service interruption.</NotificationDescription>
    </Notification>
</Story>

<Story name="Information">
    <Notification variant="information">
        <NotificationTitle>New features available</NotificationTitle>
        <NotificationDescription>
            We've released new capabilities and improvements. Check out the latest updates to explore these features.
        </NotificationDescription>
    </Notification>
</Story>

<Story name="Impersonation">
    <Notification variant="impersonation">
        {#snippet icon()}<Eye />{/snippet}
        {#snippet action()}
            <Button variant="outline" size="sm">Stop Impersonating</Button>
        {/snippet}
        <NotificationTitle>Impersonating Acme Corp</NotificationTitle>
        <NotificationDescription>
            You are viewing this organization as a global admin. Any changes you make will affect this organization directly.
        </NotificationDescription>
    </Notification>
</Story>

<Story name="With Action Only">
    <Notification variant="information">
        {#snippet action()}
            <Button variant="outline" size="sm">Learn More</Button>
        {/snippet}
        <NotificationTitle>Upgrade available</NotificationTitle>
        <NotificationDescription>
            A new version is available with improved performance and features. Click to learn more about the changes.
        </NotificationDescription>
    </Notification>
</Story>

<Story name="Default">
    <Notification>
        <NotificationTitle>System notification</NotificationTitle>
        <NotificationDescription>
            This is a standard notification with default styling. It provides important information without any specific severity level.
        </NotificationDescription>
    </Notification>
</Story>

<Story name="Single Line With Icon">
    <Notification variant="destructive">
        {#snippet icon()}<Ban />{/snippet}
        <NotificationTitle>This organization has been suspended due to non-payment.</NotificationTitle>
    </Notification>
</Story>

<Story name="Single Line With Action">
    <Notification variant="information">
        {#snippet action()}
            <Button variant="outline" size="sm">Dismiss</Button>
        {/snippet}
        <NotificationTitle>Your session will expire in 5 minutes.</NotificationTitle>
    </Notification>
</Story>
