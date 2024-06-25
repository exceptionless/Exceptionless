import { getMeQuery } from './usersApi.svelte';

export function getGravatarFromCurrentUserSrc(query?: ReturnType<typeof getMeQuery>) {
    const meQuery = query ?? getMeQuery();
    const userSrc = $derived.by(async () => {
        return meQuery.data?.email_address ? await getGravatarSrc(meQuery.data?.email_address) : null;
    });

    return userSrc;
}

export function getUserInitialsFromCurrentUserSrc(query?: ReturnType<typeof getMeQuery>) {
    const meQuery = query ?? getMeQuery();
    const initials = $derived.by(() => {
        const fullName = meQuery.data?.full_name;
        if (!fullName) {
            return 'NA';
        }

        const initials = fullName
            .split(' ')
            .map((name) => name.trim())
            .filter((name) => name.length > 0)
            .map((name) => name[0])
            .join('');

        return initials.length > 2 ? initials.substring(0, 2) : initials;
    });

    return initials;
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
