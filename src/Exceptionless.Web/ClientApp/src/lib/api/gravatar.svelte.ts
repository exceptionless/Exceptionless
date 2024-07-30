import { getMeQuery } from './usersApi.svelte';

export function getGravatarFromCurrentUser(query?: ReturnType<typeof getMeQuery>) {
    const meQuery = query ?? getMeQuery();
    const fullName = $derived(meQuery.data?.full_name);
    const emailAddress = $derived(meQuery.data?.email_address);

    return {
        get initials() {
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
