import { describe, expect, it } from 'vitest';

import { getCanonicalAppUrl } from './legacy-url';

describe('legacy app destinations', () => {
    it.each([
        ['/next/event/123?filter=a%26b#details', '/event/123?filter=a%26b#details'],
        ['/next?code=123&state=abc', '/?code=123&state=abc'],
        ['/#!/account/verify?token=a%2Bb', '/account/verify?token=a%2Bb'],
        ['/#/stack/123/mark-fixed', '/stack/123?action=fixed'],
        ['/stack/123/ignored', '/stack/123?action=ignored'],
        ['/stack/123/discarded', '/stack/123?action=discarded'],
        ['/#!/project/123/new', '/stack?project=123&type=error&mode=stack_new'],
        ['/project/123/frequent', '/stack?project=123&type=error'],
        ['/project/123/timeline', '/event?project=123&type=error'],
        ['/organization/123/dashboard', '/event?organization=123'],
        ['/account/manage?tab=notifications&projectId=123', '/account/notifications?project=123'],
        ['/organization/123/manage?tab=billing', '/organization/123/billing'],
        ['/organization/123/upgrade', '/organization/123/billing?changePlan=true'],
        ['/project/123/manage?tab=integrations', '/project/123/integrations']
    ])('translates %s to %s', (input, expected) => {
        const url = getCanonicalAppUrl(new URL(input, 'https://localhost'));
        expect(`${url.pathname}${url.search}${url.hash}`).toBe(expected);
        expect(url.origin).toBe('https://localhost');
    });

    it.each(['/nextdoor/stack/123', '/api/v2/about', '/docs', '/?code=123#state=abc', '/#details', '/#!//example.com/path', '/#!/\\example.com/path'])(
        'leaves unrelated or unsafe destinations unchanged: %s',
        (input) => {
            const url = new URL(input, 'https://localhost');
            expect(getCanonicalAppUrl(url).href).toBe(url.href);
        }
    );
});
