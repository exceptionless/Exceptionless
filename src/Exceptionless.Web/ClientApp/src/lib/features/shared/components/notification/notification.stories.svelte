<script module lang="ts">
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
        <NotificationTitle>Operation completed successfully!</NotificationTitle>
        <NotificationDescription>Your changes have been saved and are now active. All affected users will see the updates immediately.</NotificationDescription>
    </Notification>
</Story>

<Story name="Destructive">
    <Notification variant="destructive">
        <NotificationTitle>Something went wrong</NotificationTitle>
        <NotificationDescription>
            An error occurred while processing your request. Please try again or contact support if the issue persists.
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

<Story name="Default">
    <Notification>
        <NotificationTitle>System notification</NotificationTitle>
        <NotificationDescription>
            This is a standard notification with default styling. It provides important information without any specific severity level.
        </NotificationDescription>
    </Notification>
</Story>
