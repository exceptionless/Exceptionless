import { resolve } from '$app/paths';
import { enableOAuthLogin } from '$features/auth/index.svelte';
import Notifications from '@lucide/svelte/icons/bell';
import Password from '@lucide/svelte/icons/key-round';
import ExternalLogin from '@lucide/svelte/icons/link';
import Verify from '@lucide/svelte/icons/shield-check';
import Appearance from '@lucide/svelte/icons/sun-moon';
import Account from '@lucide/svelte/icons/user';
import Sessions from '@lucide/svelte/icons/users';

import type { NavigationItem, NavigationItemContext } from '../../routes.svelte';

export function routes(): NavigationItem[] {
    return [
        {
            group: 'My Account',
            href: resolve('/(app)/account/manage'),
            icon: Account,
            title: 'Account'
        },
        {
            group: 'My Account',
            href: resolve('/(app)/account/appearance'),
            icon: Appearance,
            title: 'Appearance'
        },
        {
            group: 'My Account',
            href: resolve('/(app)/account/notifications'),
            icon: Notifications,
            title: 'Notifications'
        },
        {
            group: 'My Account',
            href: resolve('/(app)/account/security'),
            icon: Password,
            title: 'Password'
        },
        {
            group: 'My Account',
            href: resolve('/(app)/account/external-logins'),
            icon: ExternalLogin,
            show: () => !!enableOAuthLogin,
            title: 'External Logins'
        },
        {
            group: 'My Account',
            href: resolve('/(app)/account/sessions'),
            icon: Sessions,
            show: () => false,
            title: 'Sessions'
        },
        {
            group: 'My Account',
            href: resolve('/(app)/account/verify'),
            icon: Verify,
            show: () => false,
            title: 'Verify'
        }
    ];
}
