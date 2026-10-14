import type { Reroute } from '@sveltejs/kit/hooks';

import { canonicalAppUrl, isSignInCallback } from '#features/navigation/legacy-links.ts';

export const reroute: Reroute = ({ url }) => {
    const canonical = canonicalAppUrl(url);
    return isSignInCallback(canonical) ? '/oauth/callback' : canonical.pathname;
};
