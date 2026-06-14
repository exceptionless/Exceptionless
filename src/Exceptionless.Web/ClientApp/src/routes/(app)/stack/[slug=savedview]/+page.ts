import { requireSavedViewSlug } from '$features/saved-views/route-guards';

import type { PageLoad } from './$types';

export const load: PageLoad = async ({ fetch, params }) => {
    await requireSavedViewSlug(fetch, 'stacks', params.slug);
};
