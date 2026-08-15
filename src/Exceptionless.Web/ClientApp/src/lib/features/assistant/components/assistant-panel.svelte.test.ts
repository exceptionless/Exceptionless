import { render, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'access-token' } }));
vi.mock('katex/dist/katex.min.css', () => ({}));

import AssistantPanel from './assistant-panel.svelte';

describe('AssistantPanel', () => {
    beforeEach(() => {
        HTMLElement.prototype.scrollTo = vi.fn();
    });

    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('initializes organization context before consuming a queued prompt', async () => {
        let requestSignal: AbortSignal | undefined;
        const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
            requestSignal = init?.signal ?? undefined;
            return new Response('data: {"type":"done"}\n\n');
        });
        vi.stubGlobal('fetch', fetchMock);

        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Analyze this stack and tell me how to fix it.' }
            }
        });

        await waitFor(() => expect(fetchMock).toHaveBeenCalledOnce());

        const request = fetchMock.mock.calls[0];
        expect(request).toBeDefined();
        expect(requestSignal?.aborted).toBe(false);
        expect(JSON.parse(request![1]?.body as string)).toMatchObject({ organization_id: 'organization-1' });
    });
});
