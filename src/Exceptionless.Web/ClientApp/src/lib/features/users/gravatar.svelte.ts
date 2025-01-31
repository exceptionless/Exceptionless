import { getInitials } from '$shared/strings';

import { getMeQuery } from './api.svelte';

export interface Gravatar {
    initials: string;
    src: Promise<null | string>;
}
export function getGravatarFromCurrentUser(query?: ReturnType<typeof getMeQuery>): Gravatar {
    const meQuery = query ?? getMeQuery();
    const fullName = $derived<string | undefined>(meQuery.data?.full_name);
    const emailAddress = $derived<string | undefined>(meQuery.data?.email_address);

    return {
        get initials() {
            return getInitials(fullName);
        },
        get src() {
            return emailAddress ? getGravatarSrc(emailAddress) : Promise.resolve(null);
        }
    };
}

export async function getGravatarSrc(emailAddress: string) {
    const hash = await getGravatarEmailHash(emailAddress);
    return `//www.gravatar.com/avatar/${hash}?default=mm&size=100&d=mp&r=g`;
}

async function getGravatarEmailHash(emailAddress: string) {
    const msgUint8 = new TextEncoder().encode(emailAddress);
    const hashBuffer = await crypto.subtle.digest('SHA-256', msgUint8);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    return hashArray.map((b) => b.toString(16).padStart(2, '0')).join('');
}
