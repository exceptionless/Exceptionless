import { describe, expect, it } from 'vitest';

import { getGravatarSrc } from './gravatar.svelte';

describe('getGravatarSrc', () => {
    it.each(['http://localhost/account/manage', 'https://localhost/account/manage'])('uses the CSP-allowed HTTPS origin when hosted at %s', async (pageUrl) => {
        // Arrange
        const emailAddress = ' Test@Example.com ';

        // Act
        const src = await getGravatarSrc(emailAddress);
        const resolvedUrl = new URL(src, pageUrl);

        // Assert
        expect(resolvedUrl.href).toBe(
            'https://www.gravatar.com/avatar/973dfe463ec85785f5f95af5ba3906eedb2d931c24e69824a89ea65dba4e813b?default=mm&size=100&d=mp&r=g'
        );
    });
});
