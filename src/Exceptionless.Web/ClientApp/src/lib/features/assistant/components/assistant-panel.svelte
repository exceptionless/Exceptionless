<script lang="ts">
    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import { page } from '$app/state';
    import ArrowRight from '@lucide/svelte/icons/arrow-right';
    import Maximize2 from '@lucide/svelte/icons/maximize-2';
    import Minimize2 from '@lucide/svelte/icons/minimize-2';
    import SquarePen from '@lucide/svelte/icons/square-pen';
    import { onDestroy, tick, untrack } from 'svelte';
    import { toast } from 'svelte-sonner';

    import { Button } from '#comp/ui/button/index.ts';
    import * as Empty from '#comp/ui/empty/index.ts';
    import * as MessageScroller from '#comp/ui/message-scroller/index.ts';
    import * as Sheet from '#comp/ui/sheet/index.ts';
    import * as Tooltip from '#comp/ui/tooltip/index.ts';
    import { accessToken } from '#features/auth/index.svelte.ts';
    import { getApiUrl } from '#shared/api/urls.ts';

    import type {
        AssistantAccessState,
        AssistantChatMessage,
        AssistantConversation,
        AssistantConversationSharingSettings,
        AssistantFeedback,
        AssistantFeedbackReason,
        AssistantProject,
        AssistantPromptRequest,
        AssistantSuggestedAction
    } from '../models';

    import { applyAssistantStreamEvent, DEFAULT_ASSISTANT_ERROR_MESSAGE, endAssistantMessage } from '../assistant-message-parts';
    import { createAssistantChatRequest } from '../assistant-request';
    import { type AssistantContext, getAssistantStarters } from '../assistant-starters';
    import { readAssistantStream } from '../assistant-stream';
    import {
        type AssistantPromptSource,
        type AssistantStopReason,
        type AssistantTelemetryContext,
        type AssistantTurnOutcome,
        AssistantTurnTelemetry,
        trackAssistantEvent
    } from '../assistant-telemetry';
    import {
        deleteAssistantConversation,
        getActiveAssistantConversationId,
        getAssistantConversationScope,
        getAssistantConversationTitle,
        loadAssistantConversations,
        saveAssistantConversation,
        setActiveAssistantConversationId
    } from '../conversation-history';
    import AssistantComposer from './assistant-composer.svelte';
    import AssistantConversationSharing from './assistant-conversation-sharing.svelte';
    import AssistantHistoryMenu from './assistant-history-menu.svelte';
    import AssistantMessage from './assistant-message.svelte';
    import AssistantUpgradeRequired from './assistant-upgrade-required.svelte';
    import ExieAvatar from './exie-avatar.svelte';

    interface Props {
        accessMessage?: string;
        accessState?: AssistantAccessState;
        collapseHref?: string;
        /** What the user is looking at, shown as removable context in the composer. */
        context?: AssistantContext;
        conversationSharing?: AssistantConversationSharingSettings;
        expandHref?: string;
        minimumPlanId?: string;
        mode?: 'page' | 'sheet';
        onAccessChanged?: () => Promise<void> | void;
        onCollapse?: () => void;
        onConversationSharingChange?: (enabled: boolean | null) => Promise<AssistantConversationSharingSettings>;
        onRetryAccess?: () => Promise<void> | void;
        open?: boolean;
        organizationId?: string;
        path?: string;
        /** The project the current page belongs to, which Exie searches until the user picks another scope. */
        projectId?: string;
        /** Projects the user can scope Exie to from the composer. */
        projects?: AssistantProject[];
        /** Whether the project list has loaded, since an empty list can also mean the user has no projects left. */
        projectsLoaded?: boolean;
        promptRequest?: AssistantPromptRequest;
        /** Scopes the locally saved chat history to the signed-in user. */
        userId?: string;
    }

    let {
        accessMessage,
        accessState = 'available',
        collapseHref,
        context,
        conversationSharing,
        expandHref,
        minimumPlanId,
        mode = 'sheet',
        onAccessChanged,
        onCollapse,
        onConversationSharingChange,
        onRetryAccess,
        open = $bindable(false),
        organizationId,
        path,
        projectId,
        projects = [],
        projectsLoaded = false,
        promptRequest,
        userId
    }: Props = $props();

    /** Space kept above the latest question when a long answer fills the view. */
    const QUESTION_SCROLL_MARGIN = 16;
    /** A restored chat older than this starts fresh, since its page context is likely stale. */
    const RESTORE_WINDOW_MS = 12 * 60 * 60 * 1000;

    let messages = $state<AssistantChatMessage[]>([]);
    let threadId = $state(createConversationId());
    let conversationId = $state(createConversationId());
    /** The project the server conversation's tool results came from: null for all projects, undefined before any request. */
    let conversationProjectId: null | string | undefined;
    let conversationOrganizationId = $state<string>();
    let conversations = $state<AssistantConversation[]>([]);
    let historyScope = $state<string>();
    let prompt = $state('');
    let editingMessageId = $state<string>();
    let contextDismissed = $state(false);
    let contextKey = $state<string>();
    /** The scope the user picked in the composer: undefined follows the page, and null searches all projects. */
    let selectedProjectId = $state<null | string>();
    let isStreaming = $state(false);
    let streamingMessageId = $state<string>();
    let announcement = $state('');
    let sharingSettings = $state<AssistantConversationSharingSettings>();
    let sharingSuppressed = $state(false);
    let sharingError = $state<string>();
    let isSavingSharing = $state(false);
    let requestedSharing = $state<boolean | null>(null);
    let composer = $state<ReturnType<typeof AssistantComposer>>();
    let scrollViewport = $state<HTMLElement | null>(null);
    /** Whether the view keeps up with the answer being written; scrolling up stops it until the next message. */
    let isFollowingAnswer = false;
    let followScrollTop = 0;
    const isSharingEnabled = $derived(sharingSettings?.enabled === true && !sharingSuppressed);
    let abortController: AbortController | undefined;
    let handledPromptRequestId: string | undefined;
    let activeTurn: AssistantTurnTelemetry | undefined;
    let lastOutcome: AssistantTurnOutcome | undefined;
    let wasVisible = false;
    let previousMode: 'page' | 'sheet' | undefined;
    let latestAssistantMessage = $derived(messages.filter((message) => message.role === 'assistant').at(-1));
    // Project pages are covered by the project scope, so only an event or stack is shown as page context.
    let resourceContext = $derived(context?.kind === 'project' ? undefined : context);
    let scopeProjectId = $derived(selectedProjectId === undefined ? projectId : (selectedProjectId ?? undefined));
    let usesPageContext = $derived(!contextDismissed && selectedProjectId === undefined);
    let starters = $derived.by(() => {
        if (resourceContext && usesPageContext) {
            return getAssistantStarters(resourceContext);
        }

        return getAssistantStarters(
            scopeProjectId
                ? {
                      kind: 'project'
                  }
                : undefined
        );
    });

    $effect(() => {
        sharingSettings = conversationSharing;
        if (conversationSharing?.enabled !== true) {
            activeTurn?.disableFullLogging();
        }
    });

    $effect(() => {
        if (accessState !== 'available') {
            untrack(() => stopStreaming('access_changed'));
        }
    });

    // A new page brings new context, so a dismissed context chip comes back and the scope follows the page again.
    // Filters and paging only change the query string, so they keep the user's choice.
    $effect(() => {
        const currentKey = `${organizationId}:${path?.split(/[?#]/)[0]}`;
        untrack(() => {
            if (currentKey !== contextKey) {
                contextKey = currentKey;
                contextDismissed = false;
                selectedProjectId = undefined;
            }
        });
    });

    // A chat can be restored before the project list loads, so recheck its project once the list arrives.
    $effect(() => {
        const availableProjects = projects;
        const loaded = projectsLoaded;
        untrack(() => {
            if (loaded && typeof selectedProjectId === 'string' && !availableProjects.some((project) => project.id === selectedProjectId)) {
                selectedProjectId = undefined;
                contextDismissed = false;
            }
        });
    });

    $effect(() => {
        const currentOrganizationId = organizationId;
        const currentScope = getAssistantConversationScope(userId, currentOrganizationId);
        if (conversationOrganizationId !== currentOrganizationId) {
            untrack(() => {
                if (messages.length > 0) {
                    trackConversationEvent('assistant.ConversationLeft', {
                        reason: 'organization_changed'
                    });
                }
                stopStreaming('organization_changed');
                // Switching organizations is in-app navigation, so save the chat under the organization it belongs to first.
                persistConversation();
            });
            resetConversation();
            conversationOrganizationId = currentOrganizationId;
            historyScope = undefined;
        }

        if (currentScope !== untrack(() => historyScope)) {
            historyScope = currentScope;
            untrack(() => restoreRecentConversation(currentScope));
        }
    });

    $effect(() => {
        const visible = mode === 'page' || open;
        const currentMode = mode;
        untrack(() => {
            if (visible !== wasVisible) {
                trackConversationEvent(visible ? 'assistant.Opened' : 'assistant.Closed');
            } else if (visible && currentMode !== previousMode) {
                trackConversationEvent('assistant.ViewChanged', {
                    previous_mode: previousMode
                });
            }
            wasVisible = visible;
            previousMode = currentMode;
        });
    });

    $effect(() => {
        if (mode === 'page' && accessState === 'available' && composer) {
            void tick().then(() => composer?.focus());
        }
    });

    // Keep up with the answer while it is written, and stop when the reader scrolls up to read something else.
    $effect(() => {
        const viewport = scrollViewport;
        if (!viewport) {
            return;
        }

        untrack(() => scrollToLatest());
        const handleScroll = () => {
            if (isFollowingAnswer && viewport.scrollTop < followScrollTop - 2) {
                isFollowingAnswer = false;
            }
        };
        const observer =
            typeof ResizeObserver === 'undefined'
                ? undefined
                : new ResizeObserver(() => {
                      if (isFollowingAnswer) {
                          scrollToLatest();
                      }
                  });
        for (const child of viewport.children) {
            observer?.observe(child);
        }

        viewport.addEventListener('scroll', handleScroll, {
            passive: true
        });
        return () => {
            observer?.disconnect();
            viewport.removeEventListener('scroll', handleScroll);
        };
    });

    onDestroy(() => {
        if (wasVisible) {
            trackConversationEvent('assistant.ConversationLeft', {
                reason: 'component_unmounted'
            });
        }
        stopStreaming('component_unmounted');
        persistConversation();
    });

    $effect(() => {
        if (
            !open ||
            accessState !== 'available' ||
            isStreaming ||
            !promptRequest ||
            !organizationId ||
            conversationOrganizationId !== organizationId ||
            promptRequest.id === handledPromptRequestId
        ) {
            return;
        }

        handledPromptRequestId = promptRequest.id;
        void submitPrompt(promptRequest.prompt, {
            source: 'queued'
        });
    });

    function cancelEdit(): void {
        editingMessageId = undefined;
        prompt = '';
        composer?.focus();
    }

    async function changeConversationSharing(enabled: boolean | null) {
        if (!onConversationSharingChange || isSavingSharing) {
            return;
        }
        sharingError = undefined;
        requestedSharing = enabled;
        isSavingSharing = true;
        if (enabled !== true) {
            sharingSuppressed = true;
            activeTurn?.disableFullLogging();
        }
        try {
            sharingSettings = await onConversationSharingChange(enabled);
            sharingSuppressed = !sharingSettings.enabled;
        } catch {
            sharingError = sharingSuppressed
                ? 'Could not save your choice. Sharing is paused on this page. Try again to save it for all devices.'
                : 'Could not save your sharing choice. Please try again.';
        } finally {
            isSavingSharing = false;
        }
    }

    function changeProjectScope(nextProjectId: string | undefined): void {
        selectedProjectId = nextProjectId === projectId ? undefined : (nextProjectId ?? null);
        // An event or stack belongs to the page's project, so another scope leaves it out.
        if (selectedProjectId !== undefined) {
            contextDismissed = true;
        }

        const projectName = nextProjectId ? (projects.find((project) => project.id === nextProjectId)?.name ?? 'this project') : 'all projects';
        announcement = `Exie will search ${projectName}.`;
        composer?.focus();
    }

    function collapseToSidePanel(): void {
        onCollapse?.();
    }

    function createConversationId(): string {
        // Match the server's Guid.ToString("N") representation for exact log correlation.
        return crypto.randomUUID().replaceAll('-', '');
    }

    function createMessage(role: AssistantChatMessage['role'], content: string): AssistantChatMessage {
        return {
            content,
            conversationId,
            id: crypto.randomUUID(),
            parts: content
                ? [
                      {
                          text: content,
                          type: 'text'
                      }
                  ]
                : [],
            role,
            tools: []
        };
    }

    function deleteConversation(id: string): void {
        if (!historyScope) {
            return;
        }

        conversations = deleteAssistantConversation(historyScope, id);
        if (id === threadId && !isStreaming) {
            resetConversation();
            setActiveAssistantConversationId(historyScope, null);
        }
    }

    function editMessage(messageId: string): void {
        const message = messages.find((item) => item.id === messageId && item.role === 'user');
        if (!message || isStreaming) {
            return;
        }

        editingMessageId = messageId;
        prompt = message.content;
        void tick().then(() =>
            composer?.focus({
                force: true
            })
        );
    }

    function getTelemetryContext(message?: AssistantChatMessage): AssistantTelemetryContext {
        return {
            assistant_message_id: message?.role === 'assistant' ? message.id : undefined,
            conversation_id: message?.conversationId ?? conversationId,
            mode,
            organization_id: conversationOrganizationId ?? organizationId,
            path: (path ?? page.url.pathname).split(/[?#]/)[0],
            project_id: scopeProjectId,
            user_message_id: message?.role === 'user' ? message.id : undefined
        };
    }

    function handleInteractOutside(event: PointerEvent): void {
        if (event.target instanceof Element && event.target.closest('[data-assistant-trigger]')) {
            event.preventDefault();
        }
    }

    function handleOpenAutoFocus(event: Event): void {
        if (accessState === 'available') {
            event.preventDefault();
            composer?.focus();
        }
    }

    function handlePageHide(): void {
        if (wasVisible || messages.length > 0) {
            trackConversationEvent('assistant.PageLeft');
        }

        persistConversation();
    }

    async function handleSuggestedAction(action: AssistantSuggestedAction, message: AssistantChatMessage): Promise<void> {
        trackAssistantEvent('assistant.SuggestedActionSelected', getTelemetryContext(message), {
            action_type: action.href ? 'navigation' : 'prompt'
        });
        if (action.href) {
            open = false;
            await goto(action.href);
            return;
        }

        await submitPrompt(action.prompt, {
            action,
            source: 'suggested_action'
        });
    }

    function persistConversation(): void {
        // Signing out clears saved chats; never write them back once the session is gone.
        if (!historyScope || messages.length === 0 || !accessToken.current) {
            return;
        }

        // A response still streaming when the page goes away is saved as stopped.
        const savedMessages = messages.map((message) => (message.id === streamingMessageId ? endAssistantMessage(message, 'stopped') : message));
        conversations = saveAssistantConversation(historyScope, {
            id: threadId,
            messages: savedMessages,
            // Record the scope the server conversation searched, which can differ from the page after navigating mid-response.
            projectId: conversationProjectId !== undefined ? conversationProjectId : (scopeProjectId ?? null),
            title: getAssistantConversationTitle(savedMessages),
            updatedAt: Date.now()
        });
        setActiveAssistantConversationId(historyScope, threadId);
    }

    async function regenerateResponse(assistantMessageId: string): Promise<void> {
        if (isStreaming) {
            return;
        }

        const assistantMessageIndex = messages.findIndex((message) => message.id === assistantMessageId && message.role === 'assistant');
        if (assistantMessageIndex < 1) {
            return;
        }

        const userMessageIndex = messages.findLastIndex((message, index) => index < assistantMessageIndex && message.role === 'user');
        if (userMessageIndex < 0) {
            return;
        }

        const source = messages[assistantMessageIndex]?.endReason ? 'retry' : 'regenerate';
        const previousConversationId = conversationId;
        trackAssistantEvent('assistant.ResponseRegenerated', getTelemetryContext(messages[assistantMessageIndex]), {
            prompt_source: source
        });
        const history = messages.slice(0, userMessageIndex + 1);
        conversationId = createConversationId();
        const replacement = createMessage('assistant', '');
        messages = [...history, replacement];
        await streamResponse(history, replacement, source, {
            previous_conversation_id: previousConversationId,
            retry_of_message_id: assistantMessageId
        });
    }

    function resetConversation(): void {
        messages = [];
        threadId = createConversationId();
        conversationId = createConversationId();
        conversationProjectId = undefined;
        prompt = '';
        editingMessageId = undefined;
        lastOutcome = undefined;
    }

    function restoreConversation(id: string): void {
        const conversation = conversations.find((item) => item.id === id);
        if (!conversation || isStreaming) {
            return;
        }

        persistConversation();
        messages = conversation.messages;
        threadId = conversation.id;
        conversationId = conversation.messages.findLast((message) => message.conversationId)?.conversationId ?? createConversationId();
        conversationProjectId = conversation.projectId;
        restoreProjectScope(conversation);
        editingMessageId = undefined;
        prompt = '';
        lastOutcome = undefined;
        if (historyScope) {
            setActiveAssistantConversationId(historyScope, threadId);
        }
        isFollowingAnswer = false;
        void tick().then(() => {
            scrollToLatest();
            composer?.focus();
        });
    }

    /** Follow-ups keep searching the projects the chat searched, since its earlier tool results came from them. */
    function restoreProjectScope(conversation: AssistantConversation): void {
        // Start from the page's scope so a project picked for another chat never carries over.
        selectedProjectId = undefined;
        contextDismissed = false;

        // Older chats did not record a scope, so they follow the current page. Their earlier tool results may
        // come from another project, so they also start a new server conversation.
        if (conversation.projectId === undefined) {
            conversationId = createConversationId();
            return;
        }

        const savedProjectId = conversation.projectId;
        if (savedProjectId && projectsLoaded && !projects.some((project) => project.id === savedProjectId)) {
            return;
        }

        selectedProjectId = (savedProjectId ?? undefined) === projectId ? undefined : savedProjectId;
        if (selectedProjectId !== undefined) {
            contextDismissed = true;
        }
    }

    function restoreRecentConversation(scope: string | undefined): void {
        conversations = scope ? loadAssistantConversations(scope) : [];
        if (!scope || messages.length > 0) {
            return;
        }

        // After New chat the user keeps a blank chat, even across reloads.
        const activeId = getActiveAssistantConversationId(scope);
        const recent = activeId === undefined ? conversations[0] : conversations.find((conversation) => conversation.id === activeId);
        if (recent && Date.now() - recent.updatedAt < RESTORE_WINDOW_MS) {
            messages = recent.messages;
            threadId = recent.id;
            conversationId = recent.messages.findLast((message) => message.conversationId)?.conversationId ?? conversationId;
            conversationProjectId = recent.projectId;
            restoreProjectScope(recent);
        }
    }

    /**
     * Scrolls down to show as much of the latest answer as fits without pushing its question above the top, so
     * the earlier conversation stays in view for short answers and a long answer still starts at its question.
     */
    function scrollToLatest(): void {
        const viewport = scrollViewport;
        if (!viewport) {
            return;
        }

        const maxScrollTop = viewport.scrollHeight - viewport.clientHeight;
        const question = [...viewport.querySelectorAll<HTMLElement>('[data-message-role="user"]')].at(-1);
        const questionScrollTop = question
            ? viewport.scrollTop + question.getBoundingClientRect().top - viewport.getBoundingClientRect().top - QUESTION_SCROLL_MARGIN
            : maxScrollTop;
        const target = Math.max(0, Math.min(maxScrollTop, questionScrollTop));
        if (target > viewport.scrollTop + 1 || !isFollowingAnswer) {
            viewport.scrollTop = target;
        }

        followScrollTop = viewport.scrollTop;
    }

    function setContextDismissed(dismissed: boolean): void {
        contextDismissed = dismissed;
        // Restoring the event or stack also restores the project it belongs to.
        if (!dismissed) {
            selectedProjectId = undefined;
        }
    }

    function setMessageFeedback(messageId: string, feedback: AssistantFeedback | undefined, reason?: AssistantFeedbackReason): void {
        const message = messages.find((message) => message.id === messageId);
        if (!message) {
            return;
        }
        const feature =
            feedback === 'helpful'
                ? 'assistant.ResponseHelpful'
                : feedback === 'not-helpful'
                  ? 'assistant.ResponseNotHelpful'
                  : 'assistant.ResponseFeedbackCleared';
        trackAssistantEvent(feature, getTelemetryContext(message), {
            feedback: feedback ?? 'cleared',
            ...(reason
                ? {
                      feedback_reason: reason
                  }
                : {})
        });
        messages = messages.map((message) =>
            message.id === messageId
                ? {
                      ...message,
                      feedback,
                      feedbackReason: feedback === 'not-helpful' ? reason : undefined
                  }
                : message
        );
        persistConversation();
    }

    function startNewChat(): void {
        if (messages.length === 0) {
            composer?.focus();
            return;
        }

        trackConversationEvent('assistant.ConversationCleared');
        stopStreaming('conversation_cleared');
        persistConversation();
        const previousThreadId = threadId;
        resetConversation();
        if (historyScope) {
            setActiveAssistantConversationId(historyScope, null);
        }
        announcement = 'Started a new chat.';
        composer?.focus();
        toast('Started a new chat', {
            action: {
                label: 'Undo',
                onClick: () => restoreConversation(previousThreadId)
            },
            duration: 8000
        });
    }

    function stopStreaming(reason: AssistantStopReason = 'user_stopped'): void {
        const outcome = activeTurn?.finish(reason, {
            is_visible: mode === 'page' || open
        });
        if (outcome) {
            lastOutcome = outcome;
        }
        abortController?.abort();
        if (!streamingMessageId) {
            return;
        }

        const stoppedMessageId = streamingMessageId;
        messages = messages.map((message) => (message.id === stoppedMessageId ? endAssistantMessage(message, 'stopped') : message));
        if (reason === 'user_stopped') {
            announcement = 'Stopped Exie’s response.';
        }
    }

    async function streamResponse(
        history: AssistantChatMessage[],
        assistantMessage: AssistantChatMessage,
        source: AssistantPromptSource,
        details: Record<string, unknown> = {}
    ): Promise<void> {
        isStreaming = true;
        streamingMessageId = assistantMessage.id;
        isFollowingAnswer = true;
        void tick().then(() => scrollToLatest());
        announcement = 'Exie is responding…';
        const controller = new AbortController();
        abortController = controller;
        const requestPath = usesPageContext ? (path ?? `${page.url.pathname}${page.url.search}`) : resolve('/(app)');
        const requestProjectId = scopeProjectId;
        const userMessage = history.at(-1)!;
        const telemetry = new AssistantTurnTelemetry(
            {
                ...getTelemetryContext(assistantMessage),
                user_message_id: userMessage.id
            },
            userMessage.content.length,
            source,
            {
                ...details,
                turn_index: history.filter((message) => message.role === 'user').length
            }
        );
        activeTurn = telemetry;
        lastOutcome = undefined;
        const request = createAssistantChatRequest(history, conversationId, organizationId, requestPath, requestProjectId);
        conversationProjectId = requestProjectId ?? null;

        try {
            const response = await fetch(getApiUrl('assistant/chat'), {
                body: JSON.stringify(request),
                headers: {
                    Authorization: `Bearer ${accessToken.current}`,
                    'Content-Type': 'application/json'
                },
                method: 'POST',
                signal: controller.signal
            });

            if (!response.ok) {
                const problem = response.headers.get('content-type')?.includes('json')
                    ? ((await response.json()) as { detail?: string; title?: string })
                    : undefined;
                const message = problem?.detail ?? problem?.title ?? `The assistant returned status ${response.status}.`;
                telemetry.fail(message, `http_${response.status}`);
                throw new Error(message);
            }

            if (!response.body) {
                throw new Error('The assistant returned an empty response.');
            }

            if (isSharingEnabled && response.headers.get('X-Exie-Full-Logging') === 'true') {
                telemetry.enableFullLogging(userMessage.content);
            }

            await readAssistantStream(response.body, async (event) => {
                if (controller.signal.aborted) {
                    return;
                }
                telemetry.observe(event);
                updateMessage(assistantMessage.id, (message) => applyAssistantStreamEvent(message, event, requestPath));
            });
        } catch (error) {
            if (controller.signal.aborted || (error instanceof DOMException && error.name === 'AbortError')) {
                return;
            }

            const errorMessage = error instanceof Error ? error.message : DEFAULT_ASSISTANT_ERROR_MESSAGE;
            telemetry.fail(errorMessage, 'request_error');
            updateMessage(assistantMessage.id, (message) => endAssistantMessage(message, 'error', errorMessage));
        } finally {
            const outcome = telemetry.finish(controller.signal.aborted ? 'user_stopped' : undefined, {
                is_visible: mode === 'page' || open
            });
            if (abortController === controller) {
                lastOutcome = outcome ?? lastOutcome;
                activeTurn = undefined;
                isStreaming = false;
                streamingMessageId = undefined;
                abortController = undefined;
                // Suggestions and actions appear once the answer ends, so follow one last time and then stop.
                void tick().then(() => {
                    if (isFollowingAnswer) {
                        scrollToLatest();
                    }

                    isFollowingAnswer = false;
                });
                const finished = messages.find((message) => message.id === assistantMessage.id);
                if (finished?.endReason === 'error') {
                    announcement = `Exie couldn’t finish the response. ${finished.errorMessage ?? ''}`.trim();
                } else if (!controller.signal.aborted) {
                    announcement = 'Exie finished responding.';
                }

                persistConversation();
            }
        }
    }

    async function submitPrompt(value = prompt, options: { action?: AssistantSuggestedAction; source?: AssistantPromptSource } = {}): Promise<void> {
        const content = value.trim();
        if (!content || isStreaming) {
            return;
        }

        prompt = '';
        const editIndex = editingMessageId ? messages.findIndex((message) => message.id === editingMessageId) : -1;
        const source = editIndex >= 0 ? 'edit' : (options.source ?? 'composer');
        editingMessageId = undefined;
        // Editing rewrites history, and tool results from another project would answer for the wrong scope,
        // so either one starts a new server conversation.
        const scopeChanged = conversationProjectId !== undefined && conversationProjectId !== (scopeProjectId ?? null);
        if (editIndex >= 0 || scopeChanged) {
            conversationId = createConversationId();
        }

        const userMessage: AssistantChatMessage = {
            ...createMessage('user', content),
            isSuggestedAction: options.source === 'suggested_action',
            suggestedActionLabel: options.action?.label,
            suggestedActionPath: options.action?.sourcePath
        };
        const assistantMessage = createMessage('assistant', '');
        const history = [...(editIndex >= 0 ? messages.slice(0, editIndex) : messages), userMessage];
        messages = [...history, assistantMessage];
        // A starter or suggestion button disappears once used, so keep focus in the composer.
        void tick().then(() => composer?.focus());
        await streamResponse(history, assistantMessage, source);
    }

    function trackConversationEvent(feature: string, details: Record<string, unknown> = {}): void {
        trackAssistantEvent(
            feature,
            {
                ...(activeTurn?.context ?? getTelemetryContext(latestAssistantMessage)),
                mode
            },
            {
                ...details,
                is_streaming: isStreaming,
                last_feedback: latestAssistantMessage?.feedback,
                last_outcome: lastOutcome,
                message_count: messages.length
            }
        );
    }

    function updateMessage(messageId: string, update: (message: AssistantChatMessage) => AssistantChatMessage): void {
        messages = messages.map((message) => (message.id === messageId ? update(message) : message));
    }
</script>

<svelte:window onpagehide={handlePageHide} />

{#snippet headerActions()}
    <Tooltip.Provider delayDuration={300}>
        <div class="flex items-center gap-0.5">
            {#if accessState === 'available'}
                <AssistantHistoryMenu
                    activeId={messages.length > 0 ? threadId : undefined}
                    {conversations}
                    disabled={!historyScope || isStreaming}
                    onDelete={deleteConversation}
                    onSelect={restoreConversation}
                />
                <Tooltip.Root>
                    <Tooltip.Trigger>
                        {#snippet child({ props })}
                            <Button {...props} aria-label="New chat" disabled={messages.length === 0} onclick={startNewChat} size="icon-sm" variant="ghost">
                                <SquarePen aria-hidden="true" />
                            </Button>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content>New chat</Tooltip.Content>
                </Tooltip.Root>
            {/if}
            {#if expandHref}
                <Tooltip.Root>
                    <Tooltip.Trigger>
                        {#snippet child({ props })}
                            <Button {...props} aria-label="Expand Exie to full page" href={expandHref} size="icon-sm" variant="ghost">
                                <Maximize2 aria-hidden="true" />
                            </Button>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content>Expand to full page</Tooltip.Content>
                </Tooltip.Root>
            {/if}
            {#if collapseHref}
                <Tooltip.Root>
                    <Tooltip.Trigger>
                        {#snippet child({ props })}
                            <Button
                                {...props}
                                aria-label="Collapse Exie to side panel"
                                href={collapseHref}
                                onclick={collapseToSidePanel}
                                size="icon-sm"
                                variant="ghost"
                            >
                                <Minimize2 aria-hidden="true" />
                            </Button>
                        {/snippet}
                    </Tooltip.Trigger>
                    <Tooltip.Content>Collapse to side panel</Tooltip.Content>
                </Tooltip.Root>
            {/if}
        </div>
    </Tooltip.Provider>
{/snippet}

{#snippet emptyState()}
    <Empty.Root class="h-full justify-center gap-6 px-6">
        <Empty.Header class="gap-2">
            <Empty.Media>
                <ExieAvatar size="lg" />
            </Empty.Media>
            <Empty.Title class="text-base">Hi, I’m Exie. How can I help?</Empty.Title>
            <Empty.Description class="max-w-80 text-sm">
                I investigate your errors with live Exceptionless data, explain what’s going wrong, and can update stacks when you ask.
            </Empty.Description>
        </Empty.Header>
        <Empty.Content class="w-full max-w-md">
            <ul aria-label="Suggested questions" class="flex w-full flex-col gap-2">
                {#each starters as starter (starter.prompt)}
                    <li>
                        <Button
                            class="group/starter h-auto w-full justify-between gap-3 rounded-xl px-3.5 py-2.5 text-left font-normal whitespace-normal"
                            onclick={() =>
                                void submitPrompt(starter.prompt, {
                                    source: 'starter'
                                })}
                            title={starter.prompt}
                            variant="outline"
                        >
                            <span class="min-w-0">{starter.label}</span>
                            <ArrowRight
                                aria-hidden="true"
                                class="text-muted-foreground size-4 shrink-0 transition-transform group-hover/starter:translate-x-0.5"
                            />
                        </Button>
                    </li>
                {/each}
            </ul>
        </Empty.Content>
    </Empty.Root>
{/snippet}

{#snippet conversation()}
    <div class="relative min-h-0 flex-1">
        {#if accessState !== 'available'}
            <AssistantUpgradeRequired {accessState} message={accessMessage} {minimumPlanId} {onAccessChanged} onRetry={onRetryAccess} {organizationId} />
        {:else if messages.length === 0}
            {@render emptyState()}
        {:else}
            <!-- The panel scrolls itself (scrollToLatest), so the provider never anchors rows or follows the bottom. -->
            <MessageScroller.Provider defaultScrollPosition="end">
                <MessageScroller.Root>
                    <MessageScroller.Viewport bind:ref={scrollViewport}>
                        <MessageScroller.Content
                            aria-busy={isStreaming}
                            aria-label="Conversation with Exie"
                            aria-live={isStreaming ? 'off' : 'polite'}
                            class={['mx-auto w-full gap-6 px-4 py-5', mode === 'page' && 'max-w-3xl']}
                        >
                            {#each messages as message, index (message.id)}
                                {@const isLast = index === messages.length - 1}
                                <!-- Skipped rendering guesses each row at 10rem, so rows resize as they appear and the chat jumps; render every row. -->
                                <MessageScroller.Item
                                    class="[contain-intrinsic-size:none] [content-visibility:visible]"
                                    data-message-role={message.role}
                                    messageId={message.id}
                                >
                                    <AssistantMessage
                                        {isLast}
                                        isStreaming={isStreaming && message.id === streamingMessageId}
                                        {message}
                                        onCopy={() =>
                                            trackAssistantEvent('assistant.MessageCopied', getTelemetryContext(message), {
                                                role: message.role
                                            })}
                                        onEdit={message.role === 'user' && !isStreaming ? () => editMessage(message.id) : undefined}
                                        onFeedback={(feedback, reason) => setMessageFeedback(message.id, feedback, reason)}
                                        onRegenerate={() => regenerateResponse(message.id)}
                                        onSuggestedAction={(action) => void handleSuggestedAction(action, message)}
                                        showSuggestedActions={message === latestAssistantMessage}
                                        suggestionsDisabled={isStreaming}
                                    />
                                </MessageScroller.Item>
                            {/each}
                        </MessageScroller.Content>
                    </MessageScroller.Viewport>
                    <MessageScroller.Button aria-label="Scroll to latest message" class="shadow-md" />
                </MessageScroller.Root>
            </MessageScroller.Provider>
        {/if}
    </div>
{/snippet}

{#snippet composerArea()}
    {#if accessState === 'available'}
        <div class="bg-background flex flex-col gap-1.5 px-3 pt-2 pb-2.5">
            <div class={['mx-auto flex w-full flex-col gap-1.5', mode === 'page' && 'max-w-3xl']}>
                <AssistantComposer
                    bind:this={composer}
                    bind:contextDismissed={() => contextDismissed, setContextDismissed}
                    bind:value={prompt}
                    context={resourceContext}
                    isEditing={!!editingMessageId}
                    {isStreaming}
                    onCancelEdit={cancelEdit}
                    onProjectChange={changeProjectScope}
                    onStop={() => stopStreaming()}
                    onSubmit={(value) => void submitPrompt(value)}
                    projectId={scopeProjectId}
                    {projects}
                />
                <div class="text-muted-foreground flex items-center justify-between gap-3 px-1 text-[11px]">
                    <span>Exie can make mistakes.<span class="hidden sm:inline">&nbsp;Check important changes.</span></span>
                    <AssistantConversationSharing
                        checked={isSharingEnabled}
                        error={sharingError}
                        isSaving={isSavingSharing}
                        onChange={changeConversationSharing}
                        onRetry={() => changeConversationSharing(requestedSharing)}
                        settings={onConversationSharingChange ? sharingSettings : undefined}
                    />
                </div>
            </div>
        </div>
    {/if}
{/snippet}

{#snippet liveRegion()}
    <div aria-live="polite" class="sr-only" role="status">{announcement}</div>
{/snippet}

{#if mode === 'page'}
    <section class="bg-background flex min-h-0 flex-1 flex-col overflow-hidden" data-assistant-page>
        <header class="flex items-center justify-between gap-3 border-b px-4 py-3">
            <div class="flex min-w-0 items-center gap-2.5">
                <ExieAvatar />
                <div class="min-w-0">
                    <h2 class="text-base leading-tight font-semibold">Exie</h2>
                    <p class="text-muted-foreground truncate text-xs">Your Exceptionless assistant</p>
                </div>
            </div>
            {@render headerActions()}
        </header>
        {@render conversation()}
        {@render composerArea()}
        {@render liveRegion()}
    </section>
{:else}
    <Sheet.Root bind:open>
        <Sheet.Content
            class="bg-background top-16! bottom-0! h-auto! w-full! gap-0 overflow-hidden sm:max-w-120!"
            data-assistant-panel
            data-tour="exie-panel"
            onInteractOutside={handleInteractOutside}
            onOpenAutoFocus={handleOpenAutoFocus}
            overlayProps={{
                class: 'top-16! bg-black/5 dark:bg-black/30 supports-backdrop-filter:backdrop-blur-[0.5px]'
            }}
            preventScroll={false}
        >
            <Sheet.Header class="flex-row items-center justify-between gap-2 border-b px-4 py-3 pr-12">
                <div class="flex min-w-0 items-center gap-2.5">
                    <ExieAvatar />
                    <div class="min-w-0">
                        <Sheet.Title class="leading-tight" level={2}>Exie</Sheet.Title>
                        <Sheet.Description class="truncate text-xs">Your Exceptionless assistant</Sheet.Description>
                    </div>
                </div>
                {@render headerActions()}
            </Sheet.Header>
            {@render conversation()}
            {@render composerArea()}
            {@render liveRegion()}
        </Sheet.Content>
    </Sheet.Root>
{/if}
