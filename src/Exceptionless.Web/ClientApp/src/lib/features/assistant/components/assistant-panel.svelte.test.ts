import { resolve } from '$app/paths';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'access-token' } }));
vi.mock('$features/billing/stripe.svelte', () => ({ isStripeEnabled: () => true }));
vi.mock('katex/dist/katex.min.css', () => ({}));
const goto = vi.hoisted(() => vi.fn(() => Promise.resolve()));
const submitFeatureUsage = vi.hoisted(() => vi.fn<(feature: string, properties?: Record<string, unknown>) => Promise<void>>().mockResolvedValue(undefined));
const submitLog = vi.hoisted(() =>
    vi.fn<(source: string, message: string, properties?: Record<string, unknown>) => Promise<void>>().mockResolvedValue(undefined)
);
vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$features/auth/exceptionless-session', () => ({ submitFeatureUsage, submitLog }));

import AssistantPanel from './assistant-panel.svelte';

describe('AssistantPanel', () => {
    beforeEach(() => {
        HTMLElement.prototype.scrollIntoView = vi.fn();
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

    it('correlates message outcomes and feedback without recording chat text', async () => {
        const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response('{"type":"text_delta","text":"The answer"}\n{"type":"done"}\n'));
        vi.stubGlobal('fetch', fetchMock);
        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                path: '/next/stack/stack-1?filter=private',
                promptRequest: { id: 'prompt-request', prompt: 'My question' }
            }
        });
        await screen.findByText('The answer');
        await screen.findByRole('button', { name: 'Good response' });
        await fireEvent.click(screen.getByRole('button', { name: 'Good response' }));
        await waitFor(() => expect(submitFeatureUsage).toHaveBeenCalledWith('assistant.ResponseHelpful', expect.anything()));

        const prompt = eventData('assistant.MessageSent');
        expect(prompt.conversation_id).toMatch(/^[0-9a-f]{32}$/);
        expect(JSON.parse(fetchMock.mock.calls[0]?.[1]?.body as string).conversation_id).toBe(prompt.conversation_id);
        expect(prompt).toMatchObject({ organization_id: 'organization-1', path: '/next/stack/stack-1', prompt_source: 'queued', role: 'user' });
        expect(eventData('assistant.ResponseCompleted')).toMatchObject({
            assistant_message_id: prompt.assistant_message_id,
            conversation_id: prompt.conversation_id,
            is_visible: true,
            outcome: 'completed'
        });
        expect(eventData('assistant.ResponseHelpful')).toMatchObject({
            assistant_message_id: prompt.assistant_message_id,
            conversation_id: prompt.conversation_id
        });
        expect(submitFeatureUsage.mock.calls.filter(([feature]) => feature === 'assistant.ResponseHelpful')).toHaveLength(1);
        const telemetry = JSON.stringify([...submitFeatureUsage.mock.calls, ...submitLog.mock.calls]);
        expect(telemetry).not.toContain('My question');
        expect(telemetry).not.toContain('The answer');
    });

    it('links a retry to the failed response across the new server conversation', async () => {
        const fetchMock = vi
            .fn()
            .mockResolvedValueOnce(new Response('{"type":"error","message":"Provider timed out"}\n{"type":"done"}\n'))
            .mockResolvedValueOnce(new Response('{"type":"text_delta","text":"Recovered answer"}\n{"type":"done"}\n'));
        vi.stubGlobal('fetch', fetchMock);
        render(AssistantPanel, {
            props: { open: true, organizationId: 'organization-1', promptRequest: { id: 'prompt-request', prompt: 'My question' } }
        });
        await screen.findByText('Provider timed out');
        await fireEvent.click(await screen.findByRole('button', { name: 'Retry' }));
        await screen.findByText('Recovered answer');
        await waitFor(() => expect(eventData('assistant.ResponseCompleted').outcome).toBe('completed'));

        const failed = eventData('assistant.ResponseFailed');
        const retried = eventData('assistant.MessageSent', 1);
        expect(retried).toMatchObject({
            previous_conversation_id: failed.conversation_id,
            prompt_source: 'retry',
            retry_of_message_id: failed.assistant_message_id
        });
        expect(retried.conversation_id).not.toBe(failed.conversation_id);
        expect(retried.conversation_id).toMatch(/^[0-9a-f]{32}$/);
        expect(JSON.parse(fetchMock.mock.calls[1]?.[1]?.body as string).conversation_id).toBe(retried.conversation_id);
        expect(submitFeatureUsage.mock.calls.filter(([feature]) => feature === 'assistant.ResponseFailed')).toHaveLength(1);
        expect(failed.error_message).toBe('Provider timed out');
    });

    it.each([undefined, 'false', 'true'])('records full chat text only when the current response enables it (%s)', async (flag) => {
        vi.stubGlobal(
            'fetch',
            vi.fn(
                async () =>
                    new Response('{"type":"text_delta","text":"Full answer"}\n{"type":"done"}\n', {
                        headers: flag === undefined ? {} : { 'X-Exie-Full-Logging': flag }
                    })
            )
        );
        render(AssistantPanel, {
            props: { open: true, organizationId: 'organization-1', promptRequest: { id: 'request-1', prompt: 'Full question' } }
        });
        await waitFor(() => expect(eventData('assistant.ResponseCompleted').outcome).toBe('completed'));
        if (flag === 'true') {
            expect(submitLog).toHaveBeenCalledWith('assistant.Prompt', 'Full question', expect.anything());
            expect(submitLog).toHaveBeenCalledWith('assistant.Response', 'Full answer', expect.anything());
            expect(submitLog).toHaveBeenCalledTimes(2);
        } else {
            expect(submitLog).not.toHaveBeenCalled();
        }
    });

    it('stops recording transcript text when full logging is disabled for the next turn', async () => {
        const fetchMock = vi
            .fn()
            .mockResolvedValueOnce(
                new Response('{"type":"text_delta","text":"First answer"}\n{"type":"done"}\n', { headers: { 'X-Exie-Full-Logging': 'true' } })
            )
            .mockResolvedValueOnce(
                new Response('{"type":"text_delta","text":"Second answer"}\n{"type":"done"}\n', { headers: { 'X-Exie-Full-Logging': 'false' } })
            );
        vi.stubGlobal('fetch', fetchMock);
        render(AssistantPanel, {
            props: { open: true, organizationId: 'organization-1', promptRequest: { id: 'request-1', prompt: 'First question' } }
        });
        await screen.findByText('First answer');
        const composer = screen.getByRole('textbox', { name: 'Message Exie' });
        await screen.findByRole('button', { name: 'Send message' });
        await fireEvent.input(composer, { target: { value: 'Second question' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Send message' }));
        await waitFor(() => expect(eventData('assistant.ResponseCompleted', 1).outcome).toBe('completed'));
        expect(submitLog).toHaveBeenCalledTimes(2);
        expect(JSON.stringify(submitLog.mock.calls)).not.toContain('Second');
    });

    it('records closing while waiting without cancelling a response that finishes in the background', async () => {
        let streamController: ReadableStreamDefaultController<Uint8Array>;
        const stream = new ReadableStream<Uint8Array>({
            start(controller) {
                streamController = controller;
            }
        });
        vi.stubGlobal(
            'fetch',
            vi.fn(async () => new Response(stream))
        );
        const props = { open: true, organizationId: 'organization-1', promptRequest: { id: 'prompt-request', prompt: 'My question' } };
        const view = render(AssistantPanel, { props });
        await screen.findByRole('button', { name: 'Stop generating' });
        await view.rerender({ ...props, open: false });
        await waitFor(() => expect(eventData('assistant.Closed').is_streaming).toBe(true));
        streamController!.enqueue(new TextEncoder().encode('{"type":"text_delta","text":"Background answer"}\n{"type":"done"}\n'));
        streamController!.close();
        await waitFor(() => expect(eventData('assistant.ResponseCompleted').is_visible).toBe(false));
        expect(submitFeatureUsage.mock.calls.some(([feature]) => feature === 'assistant.ResponseCancelled')).toBe(false);
    });

    it('records one cancellation with the original organization when organization context changes', async () => {
        vi.stubGlobal(
            'fetch',
            vi.fn(
                async (_input: RequestInfo | URL, init?: RequestInit) =>
                    new Response(
                        new ReadableStream<Uint8Array>({
                            start(controller) {
                                controller.enqueue(new TextEncoder().encode('{"type":"text_delta","text":"Partial answer"}\n'));
                                init?.signal?.addEventListener('abort', () => controller.error(new DOMException('Aborted', 'AbortError')));
                            }
                        })
                    )
            )
        );
        const view = render(AssistantPanel, {
            props: { open: true, organizationId: 'organization-1', promptRequest: { id: 'prompt-request', prompt: 'My question' } }
        });
        await screen.findByText('Partial answer');
        await view.rerender({ open: true, organizationId: 'organization-2' });
        await waitFor(() => expect(eventData('assistant.ResponseCancelled').reason).toBe('organization_changed'));
        expect(eventData('assistant.ResponseCancelled')).toMatchObject({ organization_id: 'organization-1' });
        expect(submitFeatureUsage.mock.calls.filter(([feature]) => feature === 'assistant.ResponseCancelled')).toHaveLength(1);
        expect(submitFeatureUsage.mock.calls.some(([feature]) => feature === 'assistant.ResponseCompleted')).toBe(false);
        expect(screen.queryByText('Partial answer')).toBeNull();
    });

    it('records an explicit stop once and distinguishes it from leaving the page', async () => {
        vi.stubGlobal(
            'fetch',
            vi.fn(
                async (_input: RequestInfo | URL, init?: RequestInit) =>
                    new Response(
                        new ReadableStream<Uint8Array>({
                            start(controller) {
                                controller.enqueue(new TextEncoder().encode('{"type":"text_delta","text":"Partial answer"}\n'));
                                init?.signal?.addEventListener('abort', () => controller.error(new DOMException('Aborted', 'AbortError')));
                            }
                        })
                    )
            )
        );
        render(AssistantPanel, {
            props: { open: true, organizationId: 'organization-1', promptRequest: { id: 'prompt-request', prompt: 'My question' } }
        });
        await screen.findByText('Partial answer');
        await fireEvent(window, new Event('pagehide'));
        expect(eventData('assistant.PageLeft')).toMatchObject({ is_streaming: true });
        expect(submitFeatureUsage.mock.calls.some(([feature]) => feature === 'assistant.ResponseCancelled')).toBe(false);

        await fireEvent.click(screen.getByRole('button', { name: 'Stop generating' }));
        await screen.findByRole('button', { name: 'Send message' });
        expect(eventData('assistant.ResponseCancelled')).toMatchObject({ outcome: 'cancelled', reason: 'user_stopped' });
        expect(submitFeatureUsage.mock.calls.filter(([feature]) => feature === 'assistant.ResponseCancelled')).toHaveLength(1);
        expect(screen.getByText('Partial answer')).toBeTruthy();
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
        expect(submitFeatureUsage.mock.calls.some(([feature]) => feature === 'assistant.Closed')).toBe(false);
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

    it('hides tool calls by default and toggles them locally with /tools', async () => {
        const fetchMock = vi.fn(
            async () =>
                new Response(
                    `${JSON.stringify({ arguments: '{}', tool_call_id: 'tool-1', tool_name: 'search_stacks', type: 'tool_call' })}\n${JSON.stringify({ result: '{"ok":true,"data":{"items":[]}}', tool_call_id: 'tool-1', type: 'tool_result' })}\n${JSON.stringify({ text: 'No matching stacks were found.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`
                )
        );
        vi.stubGlobal('fetch', fetchMock);

        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Find matching stacks.' }
            }
        });

        expect(await screen.findByText('No matching stacks were found.')).toBeTruthy();
        expect(screen.queryByText('Searched error stacks')).toBeNull();
        expect(screen.getByText(/\/ for commands/)).toBeTruthy();
        await screen.findByRole('button', { name: 'Send message' });

        const composer = screen.getByRole('textbox', { name: 'Message Exie' });
        await fireEvent.input(composer, { target: { value: '/' } });
        expect(screen.getByLabelText('Exie commands').textContent).toContain('/tools');
        await fireEvent.click(screen.getByText('/tools'));

        await waitFor(() => expect(screen.getByText('Searched error stacks')).toBeTruthy());
        expect(fetchMock).toHaveBeenCalledOnce();

        await fireEvent.input(composer, { target: { value: '/tools' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });

        await waitFor(() => expect(screen.queryByText('Searched error stacks')).toBeNull());
        expect(fetchMock).toHaveBeenCalledOnce();
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
        expect(eventData('assistant.SuggestedActionSelected')).toMatchObject({ action_type: 'navigation' });
        const telemetry = JSON.stringify(submitFeatureUsage.mock.calls);
        expect(telemetry).not.toContain('Open Client Setup');
        expect(telemetry).not.toContain('How do I configure');
        expect(telemetry).not.toContain(configureHref);
    });
});

function eventData(feature: string, index = 0): Record<string, unknown> {
    const properties = submitFeatureUsage.mock.calls.filter(([name]) => name === feature)[index]?.[1];
    return (properties?.exie ?? {}) as Record<string, unknown>;
}
