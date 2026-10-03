import type { Reroute } from '@sveltejs/kit';

import { canonicalAppUrl, isSignInCallback } from '$features/navigation/legacy-links';

export const reroute: Reroute = ({ url }) => {
    const canonical = canonicalAppUrl(url);
    return isSignInCallback(canonical) ? '/oauth/callback' : canonical.pathname;
};
