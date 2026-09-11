import { submitFeatureUsage, submitLog } from '$features/auth/exceptionless-session';

import type { AssistantStreamEvent } from './assistant-stream';

import { assistantToolResultFailed } from './assistant-tool-result';

const maximumMessageCharacters = 16_384;

export type AssistantPromptSource = 'composer' | 'queued' | 'regenerate' | 'retry' | 'starter' | 'suggested_action';

export type AssistantStopReason = 'access_changed' | 'component_unmounted' | 'conversation_cleared' | 'organization_changed' | 'user_stopped';
export interface AssistantTelemetryContext {
    assistant_message_id?: string;
    conversation_id: string;
    mode: 'page' | 'sheet';
    organization_id?: string;
    path?: string;
    project_id?: string;
    user_message_id?: string;
}
export type AssistantTurnOutcome = 'cancelled' | 'completed' | 'failed';

export class AssistantTurnTelemetry {
    private contentCharacters = 0;
    private errorMessage: string | undefined;
    private failureReason: string | undefined;
    private finished = false;
    private firstTextDuration: number | undefined;
    private readonly promptDetails: Record<string, unknown>;
    private receivedDone = false;
    private responseContent: string | undefined;
    private started = performance.now();
    private toolCalls = 0;
    private toolFailures = 0;

    constructor(
        readonly context: AssistantTelemetryContext,
        promptCharacters: number,
        source: AssistantPromptSource,
        details: Record<string, unknown> = {}
    ) {
        this.promptDetails = { ...details, message_characters: promptCharacters, prompt_source: source, role: 'user' };
        trackAssistantEvent('assistant.MessageSent', context, this.promptDetails);
    }

    disableFullLogging(): void {
        this.responseContent = undefined;
    }

    enableFullLogging(prompt: string): void {
        if (this.finished || this.responseContent !== undefined) {
            return;
        }

        this.responseContent = '';
        trackAssistantLog('assistant.Prompt', prompt, this.context, {
            ...this.promptDetails,
            message_truncated: prompt.length > maximumMessageCharacters
        });
    }

    fail(message: string, reason: 'request_error' | 'stream_error' | `http_${number}`): void {
        this.errorMessage ??= message.slice(0, 2048);
        this.failureReason ??= reason;
    }

    finish(stopReason?: AssistantStopReason, details: Record<string, unknown> = {}): AssistantTurnOutcome | undefined {
        if (this.finished) {
            return;
        }
        this.finished = true;
        const outcome = stopReason ? 'cancelled' : this.failureReason || !this.receivedDone || this.contentCharacters === 0 ? 'failed' : 'completed';
        const reason =
            stopReason ?? this.failureReason ?? (!this.receivedDone ? 'incomplete_stream' : this.contentCharacters === 0 ? 'empty_response' : undefined);
        const feature = { cancelled: 'assistant.ResponseCancelled', completed: 'assistant.ResponseCompleted', failed: 'assistant.ResponseFailed' }[outcome];
        const summary = {
            ...details,
            duration_ms: Math.round(performance.now() - this.started),
            error_message: this.errorMessage,
            first_text_duration_ms: this.firstTextDuration,
            message_characters: this.contentCharacters,
            outcome,
            reason,
            received_done: this.receivedDone,
            response_characters: this.contentCharacters,
            role: 'assistant',
            tool_calls: this.toolCalls,
            tool_failures: this.toolFailures
        };
        trackAssistantEvent(feature, this.context, summary);
        if (this.responseContent !== undefined) {
            trackAssistantLog('assistant.Response', this.responseContent || this.errorMessage || '', this.context, {
                ...summary,
                message_characters: this.contentCharacters || this.errorMessage?.length || 0,
                message_truncated: this.contentCharacters > maximumMessageCharacters
            });
        }
        return outcome;
    }

    observe(event: AssistantStreamEvent): void {
        if (this.finished) {
            return;
        }
        if (event.type === 'text_delta' && event.text) {
            this.contentCharacters += event.text.length;
            if (this.responseContent !== undefined) {
                this.responseContent += event.text.slice(0, Math.max(0, maximumMessageCharacters - this.responseContent.length));
            }
            this.firstTextDuration ??= Math.round(performance.now() - this.started);
        } else if (event.type === 'tool_call') {
            this.toolCalls++;
        } else if (event.type === 'tool_result' && assistantToolResultFailed(event.result)) {
            this.toolFailures++;
        } else if (event.type === 'error') {
            this.fail(event.message ?? 'Exie could not complete this request.', 'stream_error');
        } else if (event.type === 'done') {
            this.receivedDone = true;
        }
    }
}

export function trackAssistantEvent(feature: string, context: AssistantTelemetryContext, details: Record<string, unknown> = {}): void {
    // A telemetry failure must not interrupt a chat or generate another telemetry event.
    void submitFeatureUsage(feature, getProperties(context, details)).catch(() => {});
}

function getProperties(context: AssistantTelemetryContext, details: Record<string, unknown>) {
    return {
        exie: {
            ...context,
            ...details,
            schema_version: 1
        }
    };
}

function trackAssistantLog(source: string, message: string, context: AssistantTelemetryContext, details: Record<string, unknown>): void {
    void submitLog(source, message.slice(0, maximumMessageCharacters), getProperties(context, details)).catch(() => {});
}
