import IconAccount from '~icons/mdi/account';
import IconAppearance from '~icons/mdi/theme-light-dark';
import IconNotifications from '~icons/mdi/bell';
import IconPassword from '~icons/mdi/form-textbox-password';
import IconSessions from '~icons/mdi/account-multiple';

import type { NavigationItem } from '../../routes';

export const routes: NavigationItem[] = [
    {
        group: 'My Account',
        title: 'Account',
        href: '/next/account/manage',
        icon: IconAccount
    },
    {
        group: 'My Account',
        title: 'Appearance',
        href: '/next/account/appearance',
        icon: IconAppearance
    },
    {
        group: 'My Account',
        title: 'Notifications',
        href: '/next/account/notifications',
        icon: IconNotifications
    },
    {
        group: 'My Account',
        title: 'Password and authentication',
        href: '/next/account/security',
        icon: IconPassword
    },
    {
        group: 'My Account',
        title: 'Sessions',
        href: '/next/account/sessions',
        icon: IconSessions
    }
];
