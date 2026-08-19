import { DocumentVisibility } from '$shared/document-visibility.svelte';

import { accessToken } from '../auth/index.svelte';

export interface WebSocketClientOptions {
    /**
     * Base URL for WebSocket connection (e.g., 'ws://localhost:1234')
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
}

export class WebSocketClient {
    public readyState = $state<number>(WebSocket.CLOSED);

    /**
     * Lazy getter for WebSocket URL.
     * Constructed on first access. Uses baseUrl from options if provided, otherwise constructs from window.location.
     */
    public get url(): string {
        if (this._url === null) {
            if (this._options.baseUrl) {
                this._url = `${this._options.baseUrl}${this._path}`;
            } else {
                const { host, protocol } = window.location;
                const wsProtocol = protocol === 'https:' ? 'wss://' : 'ws://';
                this._url = `${wsProtocol}${host}${this._path}`;
            }
        }

        return this._url;
    }

    private _options: WebSocketClientOptions;
    private _path: string;
    private _url: null | string = null;
    private accessToken: null | string = null;
    private connectionTimeoutId: null | ReturnType<typeof setTimeout> = null;
    private hasConnectedBefore: boolean = false;
    private intentionallyClosedSockets = new WeakSet<WebSocket>();
    private reconnectAfterClose: boolean = false;
    private reconnectAttempts: number = 0;
    private reconnectTimeoutId: null | ReturnType<typeof setTimeout> = null;
    private terminalAuthFailure: boolean = false;

    private ws: null | WebSocket = null;

    /**
     * @param path - WebSocket path (default: '/api/v2/push')
     * @param options - Optional configuration
     */
    constructor(path: string = '/api/v2/push', options: WebSocketClientOptions = {}) {
        this._path = path;
        this._options = options;

        const visibility = new DocumentVisibility();

        $effect(() => {
            if (this.accessToken !== accessToken.current) {
                this.accessToken = accessToken.current;
                this.reconnectAttempts = 0; // Reset backoff on token change
                this.terminalAuthFailure = false;
                this.close();
            } else if (!visibility.visible) {
                this.close();
            }

            // Only auto-connect if we're fully closed and don't have a pending reconnect attempt
            // Don't try to connect if we're CONNECTING, OPEN, or CLOSING
            if (
                this.accessToken &&
                !this.terminalAuthFailure &&
                visibility.visible &&
                this.readyState === WebSocket.CLOSED &&
                this.reconnectTimeoutId === null
            ) {
                this.connect();
            }
        });
    }

    public close(): boolean {
        this.reconnectAfterClose = false;
        clearTimeout(this.reconnectTimeoutId!);
        this.reconnectTimeoutId = null;
        clearTimeout(this.connectionTimeoutId!);
        this.connectionTimeoutId = null;

        if (this.ws) {
            this.intentionallyClosedSockets.add(this.ws);
            this.ws.close();
            return true;
        }

        this.readyState = WebSocket.CLOSED;
        return false;
    }

    public connect() {
        if (this.ws) {
            if (this.intentionallyClosedSockets.has(this.ws)) {
                this.reconnectAfterClose = true;
            }

            return;
        }

        if (this.readyState === WebSocket.CONNECTING || this.readyState === WebSocket.OPEN) {
            return;
        }

        // isReconnect means: have we successfully connected before?
        const isReconnect: boolean = this.hasConnectedBefore;

        // Reset state
        this.readyState = WebSocket.CONNECTING;

        let socket: WebSocket;

        try {
            socket = new WebSocket(`${this.url}?access_token=${this.accessToken}`);
            this.ws = socket;
            this.onConnecting(isReconnect);
        } catch (error) {
            this.readyState = WebSocket.CLOSED;
            console.error('[WebSocketClient] Failed to create WebSocket', error);
            throw error;
        }

        // Connection timeout: if we don't connect within configured timeout, force close
        clearTimeout(this.connectionTimeoutId!);
        const timeout = this._options.connectionTimeout ?? 10000;
        this.connectionTimeoutId = setTimeout(() => {
            this.connectionTimeoutId = null;
            if (this.ws === socket && this.readyState === WebSocket.CONNECTING) {
                console.warn(`[WebSocketClient] Connection timeout after ${timeout}ms`);
                socket.close();
            }
        }, timeout);

        socket.onopen = (event: Event) => {
            if (this.ws !== socket || this.intentionallyClosedSockets.has(socket)) {
                return;
            }

            clearTimeout(this.connectionTimeoutId!);
            this.connectionTimeoutId = null;
            this.readyState = WebSocket.OPEN;
            this.reconnectAttempts = 0; // Reset backoff on successful connection
            this.hasConnectedBefore = true; // Mark that we've connected successfully
            this.onOpen(event, isReconnect);
        };

        socket.onclose = (event: CloseEvent) => {
            const wasIntentionallyClosed = this.intentionallyClosedSockets.delete(socket);
            if (this.ws !== socket) {
                return;
            }

            clearTimeout(this.connectionTimeoutId!);
            this.connectionTimeoutId = null;
            this.ws = null;

            if (wasIntentionallyClosed) {
                this.readyState = WebSocket.CLOSED;
                this.onClose(event);
                if (this.reconnectAfterClose) {
                    this.reconnectAfterClose = false;
                    this.connect();
                }

                return;
            }

            // The push endpoint accepts rejected WebSocket handshakes and closes them with 4401, so browser code
            // 1006 remains safe to treat as a transient network or service-startup failure.
            const isAuthFailure = event.code === 1008 || (event.code >= 4400 && event.code < 4500);
            if (isAuthFailure) {
                this.terminalAuthFailure = true;
                console.warn('[WebSocketClient] Auth failure detected, not reconnecting', {
                    code: event.code,
                    reason: event.reason
                });
                this.readyState = WebSocket.CLOSED;
                this.onClose(event);
                return; // Let the auth system handle redirect to login
            }

            // Calculate reconnection delay with exponential backoff
            this.readyState = WebSocket.CLOSED;
            this.reconnectAttempts++;
            const delay = this.getReconnectDelay(this.reconnectAttempts);

            this.onConnecting(true); // Always true when reconnecting after close
            this.onClose(event);

            // Schedule reconnect - clear any existing timeout first
            clearTimeout(this.reconnectTimeoutId!);
            this.reconnectTimeoutId = setTimeout(() => {
                this.reconnectTimeoutId = null;
                this.connect();
            }, delay);
        };

        socket.onmessage = (event) => {
            if (this.ws !== socket || this.intentionallyClosedSockets.has(socket)) {
                return;
            }

            this.onMessage(event);
        };

        socket.onerror = (event) => {
            if (this.ws !== socket || this.intentionallyClosedSockets.has(socket)) {
                return;
            }

            console.error('[WebSocketClient] onerror triggered', {
                event,
                readyState: this.readyState,
                reconnectAttempts: this.reconnectAttempts
            });
            this.onError(event);
        };
    }

    public onClose: (ev: CloseEvent) => void = () => {};

    public onConnecting: (isReconnect: boolean) => void = () => {};
    public onError: (ev: Event) => void = () => {};
    public onMessage: (ev: MessageEvent) => void = () => {};

    public onOpen: (ev: Event, isReconnect: boolean) => void = () => {};

    public send(data: Parameters<WebSocket['send']>[0]) {
        if (this.ws) {
            return this.ws.send(data);
        } else {
            throw new Error('INVALID_STATE_ERR : Pausing to reconnect websocket');
        }
    }

    /**
     * Calculate reconnection delay using exponential backoff
     * Can be overridden via options for testing
     */
    private getReconnectDelay(attempt: number): number {
        if (this._options.reconnectDelay) {
            return this._options.reconnectDelay(attempt);
        }

        // Default: exponential backoff 1s, 2s, 4s, 8s, 16s, max 30s
        return Math.min(1000 * Math.pow(2, attempt - 1), 30000);
    }
}
