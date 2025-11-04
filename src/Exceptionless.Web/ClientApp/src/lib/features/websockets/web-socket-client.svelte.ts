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
     * Default: 2000ms (2 seconds)
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
    private forcedClose: boolean = false;
    private hasConnectedBefore: boolean = false;
    private reconnectAttempts: number = 0;
    private reconnectTimeoutId: null | ReturnType<typeof setTimeout> = null;

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
                this.close();
            } else if (!visibility.visible) {
                this.close();
            }

            // Only auto-connect if we're fully closed and don't have a pending reconnect attempt
            // Don't try to connect if we're CONNECTING, OPEN, or CLOSING
            if (this.accessToken && visibility.visible && this.readyState === WebSocket.CLOSED && this.reconnectTimeoutId === null) {
                this.connect();
            }
        });
    }

    public close(): boolean {
        clearTimeout(this.reconnectTimeoutId!);
        this.reconnectTimeoutId = null;
        clearTimeout(this.connectionTimeoutId!);
        this.connectionTimeoutId = null;

        if (this.ws) {
            this.forcedClose = true;
            this.ws.close();
            return true;
        }

        return false;
    }

    public connect() {
        // isReconnect means: have we successfully connected before?
        const isReconnect: boolean = this.hasConnectedBefore;

        // Reset state
        this.readyState = WebSocket.CONNECTING;
        this.forcedClose = false;

        this.ws = new WebSocket(`${this.url}?access_token=${this.accessToken}`);
        this.onConnecting(isReconnect);

        // Connection timeout: if we don't connect within configured timeout, force close
        clearTimeout(this.connectionTimeoutId!);
        const timeout = this._options.connectionTimeout ?? 2000;
        this.connectionTimeoutId = setTimeout(() => {
            this.connectionTimeoutId = null;
            if (this.ws && this.readyState === WebSocket.CONNECTING) {
                console.warn(`[WebSocket] Connection timeout after ${timeout}ms`);
                this.ws.close();
            }
        }, timeout);

        this.ws.onopen = (event: Event) => {
            clearTimeout(this.connectionTimeoutId!);
            this.connectionTimeoutId = null;
            this.readyState = WebSocket.OPEN;
            this.reconnectAttempts = 0; // Reset backoff on successful connection
            this.hasConnectedBefore = true; // Mark that we've connected successfully
            this.onOpen(event, isReconnect);
        };

        this.ws.onclose = (event: CloseEvent) => {
            clearTimeout(this.connectionTimeoutId!);
            this.connectionTimeoutId = null;
            this.ws = null;

            if (this.forcedClose) {
                this.readyState = WebSocket.CLOSED;
                this.onClose(event);
                return;
            }

            // Don't retry on authentication/authorization failures
            // Code 1008 (Policy Violation) is explicit auth failure
            // Code 1006 (Abnormal Closure) during handshake could be 401/403
            // Codes 4xxx are custom application codes (e.g., 4401=401, 4403=403)
            const isAuthFailure = event.code === 1008 || (event.code === 1006 && event.wasClean === false) || (event.code >= 4400 && event.code < 4500);

            if (isAuthFailure) {
                this.readyState = WebSocket.CLOSED;
                this.onClose(event);
                return; // Let the auth system handle redirect to login
            }

            // Calculate reconnection delay with exponential backoff
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

        this.ws.onmessage = (event) => {
            this.onMessage(event);
        };

        this.ws.onerror = (event) => {
            this.onError(event);
        };
    }

    public onClose: (ev: CloseEvent) => void = () => {};
    public onConnecting: (isReconnect: boolean) => void = () => {};
    public onError: (ev: Event) => void = () => {};

    public onMessage: (ev: MessageEvent) => void = () => {};

    public onOpen: (ev: Event, isReconnect: boolean) => void = () => {};

    public send(data: ArrayBufferLike | ArrayBufferView | Blob | string) {
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
