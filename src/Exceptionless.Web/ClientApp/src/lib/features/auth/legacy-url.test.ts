import { describe, expect, it } from 'vitest';

import { getCanonicalAppUrl } from './legacy-url';

describe('legacy app destinations', () => {
    it.each([
        ['/next/event/123?filter=a%26b#details', '/event/123?filter=a%26b#details'],
        ['/next?code=123&state=abc', '/?code=123&state=abc'],
        ['/next//example.com/path?from=email', '/example.com/path?from=email'],
        ['/next?from=email#!/next/account/verify?token=a%2Bb', '/account/verify?from=email&token=a%2Bb'],
        ['/?from=email&token=outer#!/account/verify?token=inner', '/account/verify?from=email&token=inner'],
        ['/#!/account/verify?token=a%2Bb', '/account/verify?token=a%2Bb'],
        ['/#/stack/123/mark-fixed', '/stack/123?action=fixed'],
        ['/stack/123/ignored', '/stack/123?action=ignored'],
        ['/stack/123/stop-notifications', '/stack/123?action=ignored'],
        ['/stack/123/discarded', '/stack/123?action=discarded'],
        ['/#!/project/123/new', '/project/123/dashboard?type=error&view=stacks&mode=stack_new'],
        ['/project/123/frequent', '/project/123/dashboard?type=error&view=stacks'],
        ['/project/123/timeline', '/project/123/dashboard?type=error&view=events'],
        ['/project/123/dashboard', '/project/123/dashboard?view=events'],
        ['/#!/project/123/error/new', '/project/123/dashboard?type=error&view=stacks&mode=stack_new'],
        ['/project/123/log/frequent?filter=a%26b', '/project/123/dashboard?filter=a%26b&type=log&view=stacks'],
        ['/project/123/usage/timeline', '/project/123/dashboard?type=usage&view=events'],
        ['/project/123/session/timeline', '/project/123/dashboard?type=session&view=events'],
        ['/organization/123/dashboard', '/event?organization=123'],
        ['/account/manage?tab=notifications&projectId=123', '/account/notifications?project=123'],
        ['/organization/123/manage?tab=billing', '/organization/123/billing'],
        ['/organization/123/manage', '/organization/123/usage'],
        ['/organization/123/upgrade', '/organization/123/billing?changePlan=true'],
        ['/project/123/manage?tab=integrations', '/project/123/integrations']
    ])('translates %s to %s', (input, expected) => {
        const url = getCanonicalAppUrl(new URL(input, 'https://localhost'));
        expect(`${url.pathname}${url.search}${url.hash}`).toBe(expected);
        expect(url.origin).toBe('https://localhost');
        expect(getCanonicalAppUrl(url).href).toBe(url.href);
    });

    it.each(['/nextdoor/stack/123', '/api/v2/about', '/docs', '/?code=123#state=abc', '/#details', '/#!//example.com/path', '/#!/\\example.com/path'])(
        'leaves unrelated or unsafe destinations unchanged: %s',
        (input) => {
            const url = new URL(input, 'https://localhost');
            expect(getCanonicalAppUrl(url).href).toBe(url.href);
        }
    );
});
