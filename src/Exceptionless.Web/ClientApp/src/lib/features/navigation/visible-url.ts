import type { ReadonlyURL } from '$app/state';

import { page } from '$app/state';

/**
 * The URL in the address bar. Shallow routing, such as query parameter updates and detail
 * sheets, changes it without rendering a new page, so `page.url` keeps describing the page
 * that was rendered and its query string can be out of date.
 */
export function getVisibleUrl(): ReadonlyURL {
    return page.shallow?.url ?? page.url;
}
