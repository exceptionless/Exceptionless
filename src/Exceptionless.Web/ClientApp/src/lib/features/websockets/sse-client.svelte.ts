import { DocumentVisibility } from '$shared/document-visibility.svelte';

import { accessToken } from '../auth/index.svelte';

export interface SseClientOptions {
    /**
     * Base URL for SSE connection (e.g., 'http://localhost:5200')
     * If not provided, constructs from window.location
     */
    baseUrl?: string;
    /**
     * Connection timeout in milliseconds
     * Default: 10000ms (10 seconds)
     */
    connectionTimeout?: number;
    /**
     * Custom reconnection delay calculator
     * Default uses exponential backoff: 1s, 2s, 4s, 8s, 16s, max 30s
     * For testing, can return 0 to reconnect immediately
     */
    reconnectDelay?: (attempt: number) => number;
    /** Delay before probing again when push is disabled or a rolling-deploy replica returns 404. */
    unavailableRetryDelay?: number;
}

// SSE connection state constants (same values as EventSource.*)
export const SSE_CONNECTING = 0;
export const SSE_OPEN = 1;
export const SSE_CLOSED = 2;

// EventSource does not support custom Authorization headers, so the app uses fetch +
// ReadableStream to keep bearer tokens out of the query string.
export class SseClient {
    public readyState = $state<number>(SSE_CLOSED);

    /**
     * Lazy getter for SSE URL.
     */
    public get url(): string {
        if (this._url === null) {
            if (this._options.baseUrl) {
                this._url = `${this._options.baseUrl}${this._path}`;
            } else {
                const { host, protocol } = window.location;
                this._url = `${protocol}//${host}${this._path}`;
            }
        }

        return this._url;
    }

    private _options: SseClientOptions;
    private _path: string;
    private _url: null | string = null;
    private abortController: AbortController | null = null;
    private accessToken: null | string = null;
    private authFailed: boolean = false;
    private connectionTimeoutId: null | ReturnType<typeof setTimeout> = null;
    private forcedClose: boolean = false;
    private hasConnectedBefore: boolean = false;
    private pausedForVisibility: boolean = false;
    private reconnectAttempts: number = 0;
    private reconnectTimeoutId: null | ReturnType<typeof setTimeout> = null;

    private streamGeneration: number = 0;

    /**
     * @param path - SSE endpoint path (default: '/api/v2/push')
     * @param options - Optional configuration
     */
    constructor(path: string = '/api/v2/push', options: SseClientOptions = {}) {
        this._path = path;
        this._options = options;

        const visibility = new DocumentVisibility();

        $effect(() => {
            if (this.accessToken !== accessToken.current) {
                this.accessToken = accessToken.current;
                this.reconnectAttempts = 0;
                this.authFailed = false;
                this.pausedForVisibility = false;
                this.close(false);
            } else if (!visibility.visible) {
                this.pausedForVisibility = true;
                this.close(false);
            } else {
                this.pausedForVisibility = false;
            }

            if (
                this.accessToken &&
                visibility.visible &&
                this.readyState === SSE_CLOSED &&
                this.reconnectTimeoutId === null &&
                !this.authFailed &&
                !this.forcedClose
            ) {
                this.connect();
            }
        });
    }

    public close(isManual: boolean = true): boolean {
        clearTimeout(this.reconnectTimeoutId!);
        this.reconnectTimeoutId = null;
        clearTimeout(this.connectionTimeoutId!);
        this.connectionTimeoutId = null;
        this.forcedClose = isManual;

        if (this.abortController) {
            this.streamGeneration++;
            this.abortController.abort();
            this.abortController = null;
            this.readyState = SSE_CLOSED;
            return true;
        }

        return false;
    }

    public connect() {
        const isReconnect: boolean = this.hasConnectedBefore;
        const generation = ++this.streamGeneration;

        this.readyState = SSE_CONNECTING;
        this.forcedClose = false;

        this.abortController = new AbortController();
        const { signal } = this.abortController;

        this.onConnecting(isReconnect);

        // Connection timeout
        clearTimeout(this.connectionTimeoutId!);
        const timeout = this._options.connectionTimeout ?? 10000;
        this.connectionTimeoutId = setTimeout(() => {
            this.connectionTimeoutId = null;
            if (this.readyState === SSE_CONNECTING) {
                console.warn(`[SseClient] Connection timeout after ${timeout}ms`);
                this.abortController?.abort();
            }
        }, timeout);

        this.startStream(signal, isReconnect, generation);
    }

    public onClose: () => void = () => {};
    public onConnecting: (isReconnect: boolean) => void = () => {};
    public onError: (error: unknown) => void = () => {};
    public onMessage: (ev: MessageEvent) => void = () => {};
    public onOpen: (isReconnect: boolean) => void = () => {};

    /**
     * Calculate reconnection delay using exponential backoff
     */
    private getReconnectDelay(attempt: number): number {
        if (this._options.reconnectDelay) {
            return this._options.reconnectDelay(attempt);
        }

        // Default: exponential backoff 1s, 2s, 4s, 8s, 16s, max 30s
        return Math.min(1000 * Math.pow(2, attempt - 1), 30000);
    }

    private scheduleReconnect(delayOverride?: number) {
        if (this.reconnectTimeoutId !== null || this.authFailed || this.forcedClose || this.pausedForVisibility || !(this.accessToken ?? accessToken.current)) {
            this.readyState = SSE_CLOSED;
            return;
        }

        this.reconnectAttempts++;
        const delay = delayOverride ?? this.getReconnectDelay(this.reconnectAttempts);

        this.readyState = SSE_CONNECTING;
        this.onConnecting(true);
        this.onClose();

        clearTimeout(this.reconnectTimeoutId!);
        this.reconnectTimeoutId = setTimeout(() => {
            this.reconnectTimeoutId = null;
            this.connect();
        }, delay);
    }

    private async startStream(signal: AbortSignal, isReconnect: boolean, generation: number) {
        try {
            const token = this.accessToken ?? accessToken.current;
            const response = await fetch(this.url, {
                headers: {
                    Accept: 'text/event-stream',
                    Authorization: `Bearer ${token}`
                },
                signal
            });

            clearTimeout(this.connectionTimeoutId!);
            this.connectionTimeoutId = null;

            if (!response.ok) {
                // Auth failures - don't reconnect
                if (response.status === 401 || response.status === 403) {
                    console.warn('[SseClient] Auth failure, not reconnecting', { status: response.status });
                    this.authFailed = true;
                    if (response.status === 401 && accessToken.current) {
                        accessToken.current = '';
                    }

                    this.readyState = SSE_CLOSED;
                    this.onClose();
                    return;
                }

                if (response.status === 404) {
                    console.info('[SseClient] Push endpoint unavailable, probing again later');
                    this.scheduleReconnect(this._options.unavailableRetryDelay ?? 300000);
                    return;
                }

                // Rate limited
                if (response.status === 429) {
                    console.warn('[SseClient] Rate limited, will retry');
                    this.scheduleReconnect();
                    return;
                }

                throw new Error(`SSE connection failed: ${response.status}`);
            }

            if (!response.body) {
                throw new Error('SSE response has no body');
            }

            const contentType = response.headers.get('content-type')?.split(';', 1).at(0)?.trim().toLowerCase();
            if (contentType !== 'text/event-stream') {
                throw new Error(`SSE response has invalid content type: ${contentType ?? 'missing'}`);
            }

            if (generation !== this.streamGeneration) {
                this.readyState = SSE_CLOSED;
                return;
            }

            this.readyState = SSE_OPEN;
            this.reconnectAttempts = 0;
            this.hasConnectedBefore = true;
            this.onOpen(isReconnect);

            // Read the stream
            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = '';

            while (true) {
                const { done, value } = await reader.read();

                if (done) {
                    break;
                }

                if (generation !== this.streamGeneration) {
                    this.readyState = SSE_CLOSED;
                    return;
                }

                buffer += decoder.decode(value, { stream: true });

                // Process complete SSE messages (separated by double newline)
                const messages = buffer.split(/\r?\n\r?\n/);
                buffer = messages.pop() ?? '';

                for (const message of messages) {
                    if (!message.trim()) {
                        continue;
                    }

                    // Parse SSE format
                    const lines = message.split(/\r?\n/);
                    const dataLines: string[] = [];

                    for (const line of lines) {
                        if (line.startsWith('data: ')) {
                            dataLines.push(line.slice(6));
                        } else if (line.startsWith('data:')) {
                            dataLines.push(line.slice(5));
                        } else if (line.startsWith(':')) {
                            // Comment (keep-alive), ignore
                            continue;
                        }
                    }

                    if (dataLines.length > 0) {
                        const data = dataLines.join('\n');
                        // Create a MessageEvent-like object for compatibility
                        const event = new MessageEvent('message', { data });
                        this.onMessage(event);
                    }
                }
            }
        } catch (error: unknown) {
            clearTimeout(this.connectionTimeoutId!);
            this.connectionTimeoutId = null;

            if (generation !== this.streamGeneration) {
                this.readyState = SSE_CLOSED;
                return;
            }

            if (signal.aborted && (this.forcedClose || this.pausedForVisibility)) {
                // Intentional close - don't reconnect
                this.readyState = SSE_CLOSED;
                this.onClose();
                return;
            }

            if (signal.aborted) {
                // Timeout or other abort - try reconnect
                this.scheduleReconnect();
                return;
            }

            console.error('[SseClient] Stream error', error);
            this.onError(error);
        }

        // Stream ended (server closed connection) - reconnect
        if (generation === this.streamGeneration && !this.forcedClose && !this.pausedForVisibility) {
            this.scheduleReconnect();
        }
    }
}
