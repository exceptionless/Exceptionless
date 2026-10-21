import { resolve } from '$app/paths';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { env } = vi.hoisted(() => ({ env: { PUBLIC_BASE_URL: '' } }));
vi.mock('#features/shared/environment.ts', () => ({
    get PUBLIC_BASE_URL() {
        return env.PUBLIC_BASE_URL;
    }
}));

const auth = vi.hoisted(() => ({ accessToken: { current: 'access-token' as null | string } }));
vi.mock('#features/auth/index.svelte.ts', () => auth);
vi.mock('#features/billing/stripe.svelte.ts', () => ({ isStripeEnabled: () => true }));
vi.mock('katex/dist/katex.min.css', () => ({}));
const goto = vi.hoisted(() => vi.fn(() => Promise.resolve()));
const submitFeatureUsage = vi.hoisted(() => vi.fn<(feature: string, properties?: Record<string, unknown>) => Promise<void>>().mockResolvedValue(undefined));
const submitLog = vi.hoisted(() =>
    vi.fn<(source: string, message: string, properties?: Record<string, unknown>) => Promise<void>>().mockResolvedValue(undefined)
);
vi.mock('$app/navigation', () => ({ goto }));
vi.mock('#features/auth/exceptionless-session.ts', () => ({ submitFeatureUsage, submitLog }));
const toast = vi.hoisted(() => Object.assign(vi.fn(), { error: vi.fn(), success: vi.fn() }));
vi.mock('svelte-sonner', () => ({ toast }));

import AssistantPanel from './assistant-panel.svelte';

describe('AssistantPanel', () => {
    beforeEach(() => {
        env.PUBLIC_BASE_URL = '';
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

    it('records opens only when becoming visible, not when typing or changing views', async () => {
        const props = { open: true, organizationId: 'organization-1', path: '/stack' };
        const view = render(AssistantPanel, { props });
        await screen.findByRole('textbox', { name: 'Message Exie' });
        expect(submitFeatureUsage.mock.calls.filter(([feature]) => feature === 'assistant.Opened')).toHaveLength(1);

        await fireEvent.input(screen.getByRole('textbox', { name: 'Message Exie' }), { target: { value: 'Unsent draft' } });
        await view.rerender({ ...props, mode: 'page', path: '/event' });
        await waitFor(() => expect(submitFeatureUsage).toHaveBeenCalledWith('assistant.ViewChanged', expect.anything()));
        expect(submitFeatureUsage.mock.calls.filter(([feature]) => feature === 'assistant.Opened')).toHaveLength(1);

        await view.rerender({ ...props, mode: 'sheet', open: false });
        await waitFor(() => expect(submitFeatureUsage).toHaveBeenCalledWith('assistant.Closed', expect.anything()));
        await view.rerender(props);
        await waitFor(() => expect(submitFeatureUsage.mock.calls.filter(([feature]) => feature === 'assistant.Opened')).toHaveLength(2));
    });

    it('streams responses from the configured API origin', async () => {
        env.PUBLIC_BASE_URL = 'https://localhost:8443';
        const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response('{"type":"text_delta","text":"Configured origin response"}\n{"type":"done"}\n'));
        vi.stubGlobal('fetch', fetchMock);
        render(AssistantPanel, { props: { open: true, organizationId: 'organization-1', promptRequest: { id: 'origin-prompt', prompt: 'Hello' } } });
        await screen.findByText('Configured origin response');
        expect(fetchMock.mock.calls[0]?.[0]).toBe('https://localhost:8443/api/v2/assistant/chat');
    });

    it('correlates message outcomes and feedback without recording chat text', async () => {
        const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response('{"type":"text_delta","text":"The answer"}\n{"type":"done"}\n'));
        vi.stubGlobal('fetch', fetchMock);
        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                path: '/stack/stack-1?filter=private',
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
        expect(prompt).toMatchObject({ organization_id: 'organization-1', path: '/stack/stack-1', prompt_source: 'queued', role: 'user' });
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

    it.each([
        [undefined, true],
        ['false', true],
        ['true', true],
        ['true', false],
        ['true', undefined]
    ] as const)('requires both server permission (%s) and the visible sharing choice (%s)', async (flag, enabled) => {
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
            props: {
                conversationSharing: enabled === undefined ? undefined : { default_enabled: true, enabled, is_overridden: !enabled },
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Full question' }
            }
        });
        await waitFor(() => expect(eventData('assistant.ResponseCompleted').outcome).toBe('completed'));
        if (flag === 'true' && enabled) {
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
            props: {
                conversationSharing: { default_enabled: true, enabled: true, is_overridden: false },
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'First question' }
            }
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

    it('shows the inherited default and saves an explicit choice or a reset', async () => {
        const save = vi
            .fn()
            .mockResolvedValueOnce({ default_enabled: false, enabled: true, is_overridden: true })
            .mockResolvedValueOnce({ default_enabled: false, enabled: false, is_overridden: false });
        render(AssistantPanel, {
            props: {
                conversationSharing: { default_enabled: false, enabled: false, is_overridden: false },
                onConversationSharingChange: save,
                open: true,
                organizationId: 'organization-1'
            }
        });
        expect(screen.queryByRole('switch', { name: 'Share conversations to improve Exie' })).toBeNull();
        await fireEvent.click(screen.getByRole('button', { name: 'Chat sharing: Off' }));
        expect(screen.getByText(/Using the default: off/)).toBeTruthy();
        await fireEvent.click(screen.getByRole('switch', { name: 'Share conversations to improve Exie' }));
        await waitFor(() => expect(save).toHaveBeenCalledWith(true));
        await fireEvent.click(await screen.findByRole('button', { name: 'Use default (off)' }));
        await waitFor(() => expect(save).toHaveBeenLastCalledWith(null));
        await waitFor(() => expect(screen.getByRole('switch', { name: 'Share conversations to improve Exie' }).getAttribute('aria-checked')).toBe('false'));
        await fireEvent.click(screen.getByRole('button', { name: 'Chat sharing: Off' }));
        await waitFor(() => expect(screen.queryByRole('switch', { name: 'Share conversations to improve Exie' })).toBeNull());
    });

    it.each([false, true])('stops collecting the active reply and subsequent prompts when opting out (save fails: %s)', async (saveFails) => {
        let controller!: ReadableStreamDefaultController<Uint8Array>;
        const stream = new ReadableStream<Uint8Array>({
            start(value) {
                controller = value;
            }
        });
        vi.stubGlobal(
            'fetch',
            vi
                .fn()
                .mockResolvedValueOnce(new Response(stream, { headers: { 'X-Exie-Full-Logging': 'true' } }))
                .mockResolvedValueOnce(
                    new Response('{"type":"text_delta","text":"Later answer"}\n{"type":"done"}\n', { headers: { 'X-Exie-Full-Logging': 'true' } })
                )
        );
        const saved = { default_enabled: true, enabled: false, is_overridden: true };
        const save = saveFails ? vi.fn().mockRejectedValueOnce(new Error('Offline')).mockResolvedValue(saved) : vi.fn().mockResolvedValue(saved);
        render(AssistantPanel, {
            props: {
                conversationSharing: { default_enabled: true, enabled: true, is_overridden: false },
                onConversationSharingChange: save,
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'sharing-request', prompt: 'Initial question' }
            }
        });
        await waitFor(() => expect(submitLog).toHaveBeenCalledWith('assistant.Prompt', 'Initial question', expect.anything()));
        controller.enqueue(new TextEncoder().encode('{"type":"text_delta","text":"Active reply"}\n'));
        await screen.findByText('Active reply');
        await fireEvent.click(screen.getByRole('button', { name: 'Chat sharing: On' }));
        await fireEvent.click(screen.getByRole('switch', { name: 'Share conversations to improve Exie' }));
        await waitFor(() => expect(save).toHaveBeenCalledWith(false));
        if (saveFails) {
            await screen.findByText(/Sharing is paused on this page/);
            await fireEvent.click(screen.getByRole('button', { name: 'Chat sharing: Off · Not saved' }));
        }
        controller.enqueue(new TextEncoder().encode('{"type":"done"}\n'));
        controller.close();
        await waitFor(() => expect(eventData('assistant.ResponseCompleted').outcome).toBe('completed'));
        await fireEvent.input(screen.getByRole('textbox', { name: 'Message Exie' }), { target: { value: 'Later question' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Send message' }));
        await waitFor(() => expect(eventData('assistant.ResponseCompleted', 1).outcome).toBe('completed'));
        expect(submitLog).toHaveBeenCalledTimes(1);
        expect(JSON.stringify(submitLog.mock.calls)).not.toContain('Active reply');
        expect(JSON.stringify(submitLog.mock.calls)).not.toContain('Later');
        if (saveFails) {
            await fireEvent.click(screen.getByRole('button', { name: 'Chat sharing: Off · Not saved' }));
            await fireEvent.click(screen.getByRole('button', { name: 'Retry saving' }));
            await waitFor(() => expect(save).toHaveBeenCalledTimes(2));
            expect(save).toHaveBeenLastCalledWith(false);
        }
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

    it('saves a streaming chat under its organization before switching organizations', async () => {
        localStorage.clear();
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
            props: { open: true, organizationId: 'organization-1', promptRequest: { id: 'prompt-request', prompt: 'My question' }, userId: 'user-1' }
        });
        await screen.findByText('Partial answer');

        await view.rerender({ open: true, organizationId: 'organization-2', userId: 'user-1' });

        await waitFor(() => expect(screen.queryByText('Partial answer')).toBeNull());
        const saved = JSON.parse(localStorage.getItem('exie:conversations:v1:user-1:organization-1') ?? '[]') as Array<{ title: string }>;
        expect(saved.map((conversation) => conversation.title)).toEqual(['My question']);
        localStorage.clear();
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
                collapseHref: '/stack',
                mode: 'page',
                onCollapse,
                open: false,
                organizationId: 'organization-1'
            }
        });

        expect(screen.getByText('Hi, I’m Exie. How can I help?')).toBeTruthy();
        expect(screen.getByRole('button', { name: 'New chat' }).hasAttribute('disabled')).toBe(true);
        const collapseLink = screen.getByRole('link', { name: 'Collapse Exie to side panel' });
        expect(collapseLink.getAttribute('href')).toBe('/stack');

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
                expandHref: '/exie?from=%2Fstack',
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Keep this conversation.' }
            }
        });

        expect(screen.getByRole('link', { name: 'Expand Exie to full page' }).getAttribute('href')).toBe('/exie?from=%2Fstack');
        expect(await screen.findByText('The conversation is still here.')).toBeTruthy();

        await view.rerender({
            collapseHref: '/stack',
            mode: 'page',
            open: true,
            organizationId: 'organization-1',
            promptRequest: { id: 'request-1', prompt: 'Keep this conversation.' }
        });

        expect(screen.getByText('The conversation is still here.')).toBeTruthy();
        expect(screen.getByRole('log', { name: 'Conversation with Exie' })).toBeTruthy();
        expect(screen.getByRole('button', { name: 'New chat' }).hasAttribute('disabled')).toBe(false);
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
                path: '/stack/stack-a',
                promptRequest: { id: 'request-1', prompt: 'Analyze this stack.' }
            }
        });

        const action = await screen.findByRole('button', { name: 'Mark as fixed' });
        await view.rerender({
            open: true,
            organizationId: 'organization-1',
            path: '/stack/stack-b',
            promptRequest: { id: 'request-1', prompt: 'Analyze this stack.' }
        });
        await fireEvent.click(action);
        await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));

        const request = fetchMock.mock.calls[1];
        const payload = JSON.parse(request![1]?.body as string) as {
            messages: Array<{ suggested_action_path?: string }>;
            path: string;
        };
        expect(payload.path).toBe('/stack/stack-b');
        expect(payload.messages.at(-1)?.suggested_action_path).toBe('/stack/stack-a');
    });

    it('shows research steps by default', async () => {
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
        expect(screen.getByText('Searched error stacks')).toBeTruthy();
        expect(fetchMock).toHaveBeenCalledOnce();
    });

    it('keeps research collapsed to one line that names the running step', async () => {
        const toolEvents = [
            { arguments: '{}', tool_call_id: 'tool-1', tool_name: 'list_projects', type: 'tool_call' },
            { result: '{"ok":true,"data":{"items":[]}}', tool_call_id: 'tool-1', type: 'tool_result' },
            { arguments: '{}', tool_call_id: 'tool-2', tool_name: 'search_stacks', type: 'tool_call' }
        ];
        vi.stubGlobal(
            'fetch',
            vi.fn(
                async () =>
                    new Response(
                        new ReadableStream<Uint8Array>({
                            start(controller) {
                                controller.enqueue(new TextEncoder().encode(toolEvents.map((item) => `${JSON.stringify(item)}\n`).join('')));
                            }
                        })
                    )
            )
        );

        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Find matching stacks.' }
            }
        });

        // Expanding and collapsing the steps would change the chat's height while Exie works and make it jump.
        const summary = (await screen.findByText('Researching…')).closest('[data-slot="trace-line"]')!;
        expect(summary.querySelector('[data-slot="trace-description"]')?.textContent).toBe('Searching error stacks…');
        expect(summary.closest('[aria-expanded]')?.getAttribute('aria-expanded')).toBe('false');
    });

    it('lists steps in the order they ran when a change comes between lookups', async () => {
        const toolEvents = [
            { arguments: '{}', tool_call_id: 'tool-1', tool_name: 'get_stack', type: 'tool_call' },
            { result: '{"ok":true,"data":{}}', tool_call_id: 'tool-1', type: 'tool_result' },
            { arguments: '{"status":"fixed"}', tool_call_id: 'tool-2', tool_name: 'update_stack_status', type: 'tool_call' },
            { result: '{"ok":true,"data":{"changed":true}}', tool_call_id: 'tool-2', type: 'tool_result' },
            { arguments: '{}', tool_call_id: 'tool-3', tool_name: 'get_event', type: 'tool_call' },
            { result: '{"ok":true,"data":{}}', tool_call_id: 'tool-3', type: 'tool_result' },
            { text: 'Marked it fixed.', type: 'text_delta' },
            { type: 'done' }
        ];
        vi.stubGlobal(
            'fetch',
            vi.fn(async () => new Response(toolEvents.map((item) => `${JSON.stringify(item)}\n`).join('')))
        );

        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Mark this fixed.' }
            }
        });

        expect(await screen.findByText('Marked it fixed.')).toBeTruthy();
        const steps = [...document.querySelectorAll('[data-tool]')].map((element) => element.getAttribute('data-tool'));
        expect(steps).toEqual(['get_stack', 'update_stack_status', 'get_event']);
    });

    it('keeps a stopped response with an explanation instead of an empty reply', async () => {
        const fetchMock = vi.fn(
            (_input: RequestInfo | URL, init?: RequestInit) =>
                new Promise<Response>((_resolve, reject) => {
                    init?.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
                })
        );
        vi.stubGlobal('fetch', fetchMock);

        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Summarize every stack.' }
            }
        });

        await fireEvent.click(await screen.findByRole('button', { name: 'Stop generating' }));

        expect(await screen.findByText('You stopped Exie before it responded.')).toBeTruthy();
        expect(screen.getByRole('button', { name: 'Retry' })).toBeTruthy();
    });

    it('edits an earlier message and resends from that point', async () => {
        const fetchMock = vi.fn(
            async () =>
                new Response(`${JSON.stringify({ text: `Answer ${fetchMock.mock.calls.length}`, type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);

        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Original question' }
            }
        });

        expect(await screen.findByText('Answer 1')).toBeTruthy();
        await fireEvent.click(screen.getByRole('button', { name: 'Edit and resend' }));
        const composer = screen.getByRole('textbox', { name: 'Message Exie' }) as HTMLTextAreaElement;
        expect(composer.value).toBe('Original question');
        expect(screen.getByText('Editing message')).toBeTruthy();

        await fireEvent.input(composer, { target: { value: 'Edited question' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });

        expect(await screen.findByText('Answer 2')).toBeTruthy();
        expect(screen.queryByText('Original question')).toBeNull();
        expect(screen.queryByText('Answer 1')).toBeNull();
        const request = JSON.parse(String((fetchMock.mock.calls[1] as unknown as [string, RequestInit])[1].body)) as { messages: Array<{ content: string }> };
        expect(request.messages.map((message) => message.content)).toEqual(['Edited question']);
    });

    it('searches the project the user picks instead of the page context', async () => {
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Scoped answer', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);
        const props = {
            context: { kind: 'stack' as const, title: 'Checkout failure' },
            open: true,
            organizationId: 'organization-1',
            path: '/stack/5f8f8f8f8f8f8f8f8f8f8f8f',
            projectId: 'project-1',
            projects: [
                { id: 'project-1', name: 'Storefront' },
                { id: 'project-2', name: 'Billing API' }
            ]
        };
        const view = render(AssistantPanel, { props });
        const composer = await screen.findByRole('textbox', { name: 'Message Exie' });
        expect(screen.getByRole('button', { name: 'Project scope: Storefront' })).toBeTruthy();

        await fireEvent.click(screen.getByRole('button', { name: 'Project scope: Storefront' }));
        await fireEvent.click(await screen.findByRole('option', { name: 'Billing API' }));

        expect(screen.getByRole('button', { name: 'Project scope: Billing API' })).toBeTruthy();
        expect(screen.queryByTestId('assistant-context-chip')).toBeNull();
        await fireEvent.input(composer, { target: { value: 'What broke today?' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });
        await screen.findByText('Scoped answer');
        const request = JSON.parse(String((fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].body)) as {
            conversation_id: string;
            path: string;
            project_id?: string;
        };
        expect(request).toMatchObject({ path: resolve('/(app)'), project_id: 'project-2' });

        await fireEvent.click(screen.getByRole('button', { name: 'Project scope: Billing API' }));
        await fireEvent.click(await screen.findByRole('option', { name: 'All projects' }));
        await fireEvent.input(composer, { target: { value: 'And across everything?' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });
        await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
        const allProjectsRequest = JSON.parse(String((fetchMock.mock.calls[1] as unknown as [string, RequestInit])[1].body)) as {
            conversation_id: string;
            project_id?: string;
        };
        expect(allProjectsRequest.project_id).toBeUndefined();
        // Tool results from Billing API must not answer for all projects, so the server conversation starts over.
        expect(allProjectsRequest.conversation_id).not.toBe(request.conversation_id);

        await fireEvent.input(composer, { target: { value: 'Which one is newest?' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });
        await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
        const sameScopeRequest = JSON.parse(String((fetchMock.mock.calls[2] as unknown as [string, RequestInit])[1].body)) as { conversation_id: string };
        expect(sameScopeRequest.conversation_id).toBe(allProjectsRequest.conversation_id);

        await view.rerender({ ...props, path: '/stack/6f8f8f8f8f8f8f8f8f8f8f8f' });
        expect(await screen.findByRole('button', { name: 'Project scope: Storefront' })).toBeTruthy();
        expect(screen.getByTestId('assistant-context-chip')).toBeTruthy();
    });

    it('keeps the picked project when only filters or paging change', async () => {
        const props = {
            open: true,
            organizationId: 'organization-1',
            path: '/stack/all?filter=status%3Aopen',
            projects: [
                { id: 'project-1', name: 'Storefront' },
                { id: 'project-2', name: 'Billing API' }
            ]
        };
        const view = render(AssistantPanel, { props });
        await fireEvent.click(await screen.findByRole('button', { name: 'Project scope: All projects' }));
        await fireEvent.click(await screen.findByRole('option', { name: 'Billing API' }));

        await view.rerender({ ...props, path: '/stack/all?filter=status%3Aopen&page=2' });
        expect(screen.getByRole('button', { name: 'Project scope: Billing API' })).toBeTruthy();

        await view.rerender({ ...props, path: '/stack/most-frequent-errors' });
        expect(await screen.findByRole('button', { name: 'Project scope: All projects' })).toBeTruthy();
    });

    it('restores the project a saved chat searched', async () => {
        localStorage.clear();
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Billing answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);
        const props = {
            open: true,
            organizationId: 'organization-1',
            path: '/project/5f8f8f8f8f8f8f8f8f8f8f8f/issues',
            projectId: 'project-1',
            projects: [
                { id: 'project-1', name: 'Storefront' },
                { id: 'project-2', name: 'Billing API' }
            ],
            projectsLoaded: true,
            userId: 'user-1'
        };
        const first = render(AssistantPanel, { props });
        const composer = await screen.findByRole('textbox', { name: 'Message Exie' });
        await fireEvent.click(screen.getByRole('button', { name: 'Project scope: Storefront' }));
        await fireEvent.click(await screen.findByRole('option', { name: 'Billing API' }));
        await fireEvent.input(composer, { target: { value: 'What broke in billing?' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });
        expect(await screen.findByText('Billing answer.')).toBeTruthy();
        first.unmount();

        render(AssistantPanel, { props });

        expect(await screen.findByText('Billing answer.')).toBeTruthy();
        expect(screen.getByRole('button', { name: 'Project scope: Billing API' })).toBeTruthy();
        localStorage.clear();
    });

    it('saves chats per user and organization and restores the latest one', async () => {
        localStorage.clear();
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Saved answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);

        const first = render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Remember this chat.' },
                userId: 'user-1'
            }
        });
        expect(await screen.findByText('Saved answer.')).toBeTruthy();
        first.unmount();

        render(AssistantPanel, { props: { open: true, organizationId: 'organization-1', userId: 'user-1' } });

        expect(await screen.findByText('Saved answer.')).toBeTruthy();
        await fireEvent.click(screen.getByRole('button', { name: 'Recent chats' }));
        const savedChat = await waitFor(() => {
            const item = document.querySelector('[aria-current="true"]');
            expect(item?.textContent).toContain('Remember this chat.');
            return item;
        });
        expect(savedChat).toBeTruthy();
        localStorage.clear();
    });

    it('does not save the chat again after the user signs out', async () => {
        localStorage.clear();
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Private answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);

        const view = render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Private question' },
                userId: 'user-1'
            }
        });
        expect(await screen.findByText('Private answer.')).toBeTruthy();

        localStorage.clear();
        auth.accessToken.current = null;
        try {
            view.unmount();
            expect(localStorage.length).toBe(0);
        } finally {
            auth.accessToken.current = 'access-token';
        }
    });

    it('starts a new chat that can be undone', async () => {
        localStorage.clear();
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'First answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);

        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'First question' },
                userId: 'user-1'
            }
        });
        expect(await screen.findByText('First answer.')).toBeTruthy();

        await fireEvent.click(screen.getByRole('button', { name: 'New chat' }));

        expect(screen.queryByText('First answer.')).toBeNull();
        expect(screen.getByText('Hi, I’m Exie. How can I help?')).toBeTruthy();
        const [, options] = toast.mock.calls.at(-1) as [string, { action: { onClick: () => void } }];
        options.action.onClick();
        expect(await screen.findByText('First answer.')).toBeTruthy();
        localStorage.clear();
    });

    it('saves the project a response searched even after moving to another project mid-response', async () => {
        localStorage.clear();
        let finishResponse: () => void = () => {};
        vi.stubGlobal(
            'fetch',
            vi.fn(
                async () =>
                    new Response(
                        new ReadableStream<Uint8Array>({
                            start(controller) {
                                controller.enqueue(new TextEncoder().encode(`${JSON.stringify({ text: 'Storefront answer.', type: 'text_delta' })}\n`));
                                finishResponse = () => {
                                    controller.enqueue(new TextEncoder().encode(`${JSON.stringify({ type: 'done' })}\n`));
                                    controller.close();
                                };
                            }
                        })
                    )
            )
        );
        const projects = [
            { id: 'project-1', name: 'Storefront' },
            { id: 'project-2', name: 'Billing API' }
        ];
        const storefront = { open: true, organizationId: 'organization-1', path: '/project/1/issues', projectId: 'project-1', projects, userId: 'user-1' };
        const billing = { ...storefront, path: '/project/2/issues', projectId: 'project-2' };
        const view = render(AssistantPanel, { props: { ...storefront, promptRequest: { id: 'request-1', prompt: 'What broke?' } } });
        expect(await screen.findByText('Storefront answer.')).toBeTruthy();

        await view.rerender(billing);
        finishResponse();
        await waitFor(() => expect(screen.queryByRole('button', { name: 'Stop generating' })).toBeNull());
        view.unmount();

        const saved = JSON.parse(localStorage.getItem('exie:conversations:v1:user-1:organization-1') ?? '[]') as Array<{ projectId?: null | string }>;
        expect(saved[0]?.projectId).toBe('project-1');
        localStorage.clear();
    });

    it('follows the page when a restored chat searched a project that is no longer available', async () => {
        localStorage.clear();
        const message = {
            content: 'Earlier answer.',
            conversationId: 'conversation-1',
            id: 'assistant-1',
            parts: [{ text: 'Earlier answer.', type: 'text' }],
            role: 'assistant',
            tools: []
        };
        localStorage.setItem(
            'exie:conversations:v1:user-1:organization-1',
            JSON.stringify([{ id: 'chat-1', messages: [message], projectId: 'deleted-project', title: 'Earlier question', updatedAt: Date.now() }])
        );
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Page answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);
        const props = {
            open: true,
            organizationId: 'organization-1',
            path: '/project/5f8f8f8f8f8f8f8f8f8f8f8f/issues',
            projectId: 'project-1',
            userId: 'user-1'
        };
        const view = render(AssistantPanel, { props });
        expect(await screen.findByText('Earlier answer.')).toBeTruthy();

        await view.rerender({
            ...props,
            projects: [
                { id: 'project-1', name: 'Storefront' },
                { id: 'project-2', name: 'Billing API' }
            ],
            projectsLoaded: true
        });

        expect(await screen.findByRole('button', { name: 'Project scope: Storefront' })).toBeTruthy();
        const composer = screen.getByRole('textbox', { name: 'Message Exie' });
        await fireEvent.input(composer, { target: { value: 'What else broke?' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });
        await screen.findByText('Page answer.');
        const request = JSON.parse(String((fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].body)) as {
            conversation_id: string;
            project_id?: string;
        };
        expect(request.project_id).toBe('project-1');
        expect(request.conversation_id).not.toBe('conversation-1');
        localStorage.clear();
    });

    it('follows the page when a restored chat searched a project and no projects remain', async () => {
        localStorage.clear();
        const message = {
            content: 'Earlier answer.',
            conversationId: 'conversation-1',
            id: 'assistant-1',
            parts: [{ text: 'Earlier answer.', type: 'text' }],
            role: 'assistant',
            tools: []
        };
        localStorage.setItem(
            'exie:conversations:v1:user-1:organization-1',
            JSON.stringify([{ id: 'chat-1', messages: [message], projectId: 'deleted-project', title: 'Earlier question', updatedAt: Date.now() }])
        );
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Fresh answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);
        const props = { open: true, organizationId: 'organization-1', path: '/stack/all', userId: 'user-1' };
        const view = render(AssistantPanel, { props });
        expect(await screen.findByText('Earlier answer.')).toBeTruthy();

        // The list loaded and is empty, which is different from not having loaded yet.
        await view.rerender({ ...props, projects: [], projectsLoaded: true });

        const composer = screen.getByRole('textbox', { name: 'Message Exie' });
        await fireEvent.input(composer, { target: { value: 'What else broke?' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });
        await screen.findByText('Fresh answer.');
        const request = JSON.parse(String((fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].body)) as {
            conversation_id: string;
            project_id?: string;
        };
        expect(request.project_id).toBeUndefined();
        expect(request.conversation_id).not.toBe('conversation-1');
        localStorage.clear();
    });

    it('follows the page when opening an older chat that did not record a project', async () => {
        localStorage.clear();
        const message = {
            content: 'Legacy answer.',
            conversationId: 'legacy-conversation',
            id: 'assistant-1',
            parts: [{ text: 'Legacy answer.', type: 'text' }],
            role: 'assistant',
            tools: []
        };
        // Older than the restore window, so it only opens from Recent chats.
        localStorage.setItem(
            'exie:conversations:v1:user-1:organization-1',
            JSON.stringify([{ id: 'chat-legacy', messages: [message], title: 'Legacy question', updatedAt: Date.now() - 13 * 60 * 60 * 1000 }])
        );
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Page answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);
        render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                path: '/project/5f8f8f8f8f8f8f8f8f8f8f8f/issues',
                projectId: 'project-1',
                projects: [
                    { id: 'project-1', name: 'Storefront' },
                    { id: 'project-2', name: 'Billing API' }
                ],
                projectsLoaded: true,
                userId: 'user-1'
            }
        });
        await fireEvent.click(await screen.findByRole('button', { name: 'Project scope: Storefront' }));
        await fireEvent.click(await screen.findByRole('option', { name: 'Billing API' }));
        expect(screen.getByRole('button', { name: 'Project scope: Billing API' })).toBeTruthy();

        await fireEvent.click(screen.getByRole('button', { name: 'Recent chats' }));
        await fireEvent.click(await screen.findByText('Legacy question'));

        expect(await screen.findByText('Legacy answer.')).toBeTruthy();
        expect(screen.getByRole('button', { name: 'Project scope: Storefront' })).toBeTruthy();
        const composer = screen.getByRole('textbox', { name: 'Message Exie' });
        await fireEvent.input(composer, { target: { value: 'What else broke?' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });
        await screen.findByText('Page answer.');
        const request = JSON.parse(String((fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].body)) as {
            conversation_id: string;
            project_id?: string;
        };
        expect(request.project_id).toBe('project-1');
        expect(request.conversation_id).not.toBe('legacy-conversation');
        localStorage.clear();
    });

    it('opens the newest chat when the remembered chat has expired', async () => {
        localStorage.clear();
        const message = (text: string) => ({ content: text, id: crypto.randomUUID(), parts: [{ text, type: 'text' }], role: 'assistant', tools: [] });
        localStorage.setItem(
            'exie:conversations:v1:user-1:organization-1',
            JSON.stringify([
                { id: 'chat-recent', messages: [message('Recent answer.')], title: 'Recent question', updatedAt: Date.now() - 60_000 },
                { id: 'chat-expired', messages: [message('Expired answer.')], title: 'Expired question', updatedAt: Date.now() - 31 * 24 * 60 * 60 * 1000 }
            ])
        );
        localStorage.setItem('exie:conversations:v1:active:user-1:organization-1', 'chat-expired');

        render(AssistantPanel, { props: { open: true, organizationId: 'organization-1', userId: 'user-1' } });

        expect(await screen.findByText('Recent answer.')).toBeTruthy();
        expect(screen.queryByText('Expired answer.')).toBeNull();
        localStorage.clear();
    });

    it('does not reuse a restored chat server context before project access is known', async () => {
        localStorage.clear();
        const message = {
            content: 'Billing answer.',
            conversationId: 'billing-conversation',
            id: 'assistant-1',
            parts: [{ text: 'Billing answer.', type: 'text' }],
            role: 'assistant',
            tools: []
        };
        localStorage.setItem(
            'exie:conversations:v1:user-1:organization-1',
            JSON.stringify([{ id: 'chat-1', messages: [message], projectId: 'project-2', title: 'Billing question', updatedAt: Date.now() }])
        );
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Fresh answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);

        // The project list has not loaded, so access to project-2 is not confirmed yet.
        render(AssistantPanel, {
            props: { open: true, organizationId: 'organization-1', path: '/project/1/issues', projectId: 'project-1', userId: 'user-1' }
        });
        expect(await screen.findByText('Billing answer.')).toBeTruthy();
        const composer = screen.getByRole('textbox', { name: 'Message Exie' });
        await fireEvent.input(composer, { target: { value: 'What else broke?' } });
        await fireEvent.keyDown(composer, { key: 'Enter' });
        await screen.findByText('Fresh answer.');

        const request = JSON.parse(String((fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].body)) as { conversation_id: string };
        expect(request.conversation_id).not.toBe('billing-conversation');
        localStorage.clear();
    });

    it('keeps a new chat blank after the panel reloads', async () => {
        localStorage.clear();
        const fetchMock = vi.fn(
            async () => new Response(`${JSON.stringify({ text: 'Earlier answer.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`)
        );
        vi.stubGlobal('fetch', fetchMock);

        const first = render(AssistantPanel, {
            props: {
                open: true,
                organizationId: 'organization-1',
                promptRequest: { id: 'request-1', prompt: 'Earlier question' },
                userId: 'user-1'
            }
        });
        expect(await screen.findByText('Earlier answer.')).toBeTruthy();
        await fireEvent.click(screen.getByRole('button', { name: 'New chat' }));
        first.unmount();

        render(AssistantPanel, { props: { open: true, organizationId: 'organization-1', userId: 'user-1' } });

        expect(await screen.findByText('Hi, I’m Exie. How can I help?')).toBeTruthy();
        expect(screen.queryByText('Earlier answer.')).toBeNull();
        localStorage.clear();
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
