import { goto } from '$app/navigation';
import { match } from '$app/paths';

/**
 * Follows a same-origin redirect. `goto` only navigates to app routes, so any other path,
 * such as an API endpoint or the documentation, is loaded as a full page instead.
 */
export async function gotoRedirect(url: string, { replace = false }: { replace?: boolean } = {}): Promise<void> {
    if (await match(url)) {
        await goto(url, { replace });
    } else if (replace) {
        window.location.replace(url);
    } else {
        window.location.assign(url);
    }
}
