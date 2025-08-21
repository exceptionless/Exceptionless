import Description from './notification-description.svelte';
import Title from './notification-title.svelte';
import Root from './notification.svelte';
export { type NotificationVariant, notificationVariants } from './notification.svelte';

export {
    Description,
    //
    Root as Notification,
    Description as NotificationDescription,
    Title as NotificationTitle,
    Root,
    Title
};
