import { resolve } from '$app/paths';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'access-token' } }));
vi.mock('$features/billing/stripe.svelte', () => ({ isStripeEnabled: () => true }));
vi.mock('katex/dist/katex.min.css', () => ({}));
const goto = vi.hoisted(() => vi.fn(() => Promise.resolve()));
vi.mock('$app/navigation', () => ({ goto }));

import AssistantPanel from './assistant-panel.svelte';

describe('AssistantPanel', () => {
    beforeEach(() => {
        HTMLElement.prototype.scrollTo = vi.fn();
    });

    it('renders unavailable access without recursively updating message state', () => {
        expect(() =>
            render(AssistantPanel, {
                props: {
                    accessState: 'upgrade-required',
                    open: true,
                    organizationId: 'organization-1'
                }
            })
        ).not.toThrow();

        expect(screen.getByText('Bring Exie onto your team')).toBeTruthy();
    });

    it('renders as a full-page chat and opens the side panel when collapsed', async () => {
        const onCollapse = vi.fn();
        render(AssistantPanel, {
            props: {
                collapseHref: '/next/stack',
                mode: 'page',
                onCollapse,
                open: false,
                organizationId: 'organization-1'
            }
        });

        expect(screen.getByRole('log', { name: 'Conversation with Exie' })).toBeTruthy();
        expect(screen.getByRole('button', { name: 'Clear conversation' }).hasAttribute('disabled')).toBe(true);
        const collapseLink = screen.getByRole('link', { name: 'Collapse Exie to side panel' });
        expect(collapseLink.getAttribute('href')).toBe('/next/stack');

        collapseLink.addEventListener('click', (event) => event.preventDefault());
        await fireEvent.click(collapseLink);

        expect(onCollapse).toHaveBeenCalledOnce();
    });

    it('keeps the conversation when expanding the side panel to the full page', async () => {
        const fetchMock = vi.fn(
            async () =>
                new Response(`${JSON.stringify({ text: 'The conversation is still here.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);

        const view = render(AssistantPanel, {
            props: {
                expandHref: '/next/exie?from=%2Fnext%2Fstack',
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Keep this conversation.' }
            }
        });

        expect(screen.getByRole('link', { name: 'Expand Exie to full page' }).getAttribute('href')).toBe('/next/exie?from=%2Fnext%2Fstack');
        expect(await screen.findByText('The conversation is still here.')).toBeTruthy();

        await view.rerender({
            collapseHref: '/next/stack',
            mode: 'page',
            open: true,
            organizationId: 'organization-1',
            promptRequest: { id: 'request-1', prompt: 'Keep this conversation.' }
        });

        expect(screen.getByText('The conversation is still here.')).toBeTruthy();
        expect(screen.getByRole('button', { name: 'Clear conversation' }).hasAttribute('disabled')).toBe(false);
        expect(fetchMock).toHaveBeenCalledOnce();
    });

    afterEach(() => {
        vi.unstubAllGlobals();
        goto.mockClear();
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

    it('retains the page path where a suggested action was created', async () => {
        const fetchMock = vi.fn(async (...args: [RequestInfo | URL, RequestInit?]) => {
            void args;
            if (fetchMock.mock.calls.length === 1) {
                return new Response(
                    '{"type":"suggested_actions","suggested_actions":[{"label":"Mark as fixed","prompt":"Please mark this stack fixed"}]}\n' +
                        '{"type":"done"}\n'
                );
            }

            return new Response('{"type":"done"}\n');
        });
        vi.stubGlobal('fetch', fetchMock);

        const view = render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                path: '/next/stack/stack-a',
                promptRequest: { id: 'request-1', prompt: 'Analyze this stack.' }
            }
        });

        const action = await screen.findByRole('button', { name: 'Mark as fixed' });
        await view.rerender({
            open: true,
            organizationId: 'organization-1',
            path: '/next/stack/stack-b',
            promptRequest: { id: 'request-1', prompt: 'Analyze this stack.' }
        });
        await fireEvent.click(action);
        await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));

        const request = fetchMock.mock.calls[1];
        const payload = JSON.parse(request![1]?.body as string) as {
            messages: Array<{ suggested_action_path?: string }>;
            path: string;
        };
        expect(payload.path).toBe('/next/stack/stack-b');
        expect(payload.messages.at(-1)?.suggested_action_path).toBe('/next/stack/stack-a');
    });

    it('navigates a validated setup action without submitting another prompt', async () => {
        const configureHref = resolve('/(app)/project/[projectId]/configure', {
            projectId: 'project-1'
        });
        const fetchMock = vi.fn(
            async () =>
                new Response(
                    `${JSON.stringify({
                        suggested_actions: [
                            {
                                href: configureHref,
                                label: 'Open Client Setup',
                                prompt: 'How do I configure this project to start sending events?'
                            }
                        ],
                        type: 'suggested_actions'
                    })}\n${JSON.stringify({ type: 'done' })}\n`
                )
        );
        vi.stubGlobal('fetch', fetchMock);

        const view = render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                projectId: 'project-1',
                promptRequest: { id: 'request-1', prompt: 'How do I configure this project?' }
            }
        });

        const action = await screen.findByRole('button', { name: 'Open Client Setup' });
        await view.rerender({
            open: true,
            organizationId: 'organization-1',
            projectId: 'project-2',
            promptRequest: { id: 'request-1', prompt: 'How do I configure this project?' }
        });
        await fireEvent.click(action);

        await waitFor(() => expect(goto).toHaveBeenCalledWith(configureHref));
        expect(fetchMock).toHaveBeenCalledOnce();
    });
});
