import { derived } from 'svelte/store';
import { getMeQuery } from './usersApi';

export function getGravatarFromCurrentUserSrc(query?: ReturnType<typeof getMeQuery>) {
    return derived(query ?? getMeQuery(), async ($userResponse) => {
        return $userResponse.data?.email_address ? await getGravatarSrc($userResponse.data?.email_address) : null;
    });
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
