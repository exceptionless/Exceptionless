import { DocumentVisibility } from '$lib/helpers/document-visibility.svelte';
import { accessToken } from './auth.svelte';

export class WebSocketClient {
    private accessToken: string | null = null;

    public reconnectInterval: number = 1000;
    public timeoutInterval: number = 2000;
    public readyState: number = WebSocket.CLOSED;
    public url: string;

    private forcedClose: boolean = false;
    private timedOut: boolean = false;
    private ws: WebSocket | null = null;

    public onConnecting: (isReconnect: boolean) => void = () => {};
    public onOpen: (ev: Event, isReconnect: boolean) => void = () => {};
    public onMessage: (ev: MessageEvent) => void = () => {};
    public onError: (ev: Event) => void = () => {};
    public onClose: (ev: CloseEvent) => void = () => {};

    constructor(path: string = '/api/v2/push') {
        const { host, protocol } = window.location;
        const wsProtocol = protocol === 'https:' ? 'wss://' : 'ws://';
        this.url = `${wsProtocol}${host}${path}`;

        const visibility = new DocumentVisibility();

        $effect(() => {
            if (this.accessToken !== accessToken.value) {
                this.accessToken = accessToken.value;
                this.close();
            } else if (!visibility.visible) {
                this.close();
            }

            if (this.accessToken && visibility.visible && (this.readyState === WebSocket.CLOSING || this.readyState === WebSocket.CLOSED)) {
                this.connect();
            }
        });
    }

    public connect(reconnectAttempt: boolean = true) {
        const isReconnect: boolean = this.forcedClose;

        // Reset state
        this.readyState = WebSocket.CONNECTING;
        this.forcedClose = false;

        this.ws = new WebSocket(`${this.url}?access_token=${this.accessToken}`);
        this.onConnecting(isReconnect);

        const localWs = this.ws;
        const timeout = setTimeout(() => {
            this.timedOut = true;
            localWs.close();
            this.timedOut = false;
        }, this.timeoutInterval);

        this.ws.onopen = (event: Event) => {
            clearTimeout(timeout);

            this.readyState = WebSocket.OPEN;
            reconnectAttempt = false;
            this.onOpen(event, isReconnect);
        };

        this.ws.onclose = (event: CloseEvent) => {
            clearTimeout(timeout);
            this.ws = null;

            if (this.forcedClose) {
                this.readyState = WebSocket.CLOSED;
                this.onClose(event);
            } else {
                this.readyState = WebSocket.CONNECTING;
                this.onConnecting(isReconnect);
                if (!reconnectAttempt && !this.timedOut) {
                    this.onClose(event);
                }
                setTimeout(() => {
                    this.connect(true);
                }, this.reconnectInterval);
            }
        };
        this.ws.onmessage = (event) => {
            this.onMessage(event);
        };
        this.ws.onerror = (event) => {
            this.onError(event);
        };
    }

    public send(data: string | ArrayBufferLike | Blob | ArrayBufferView) {
        if (this.ws) {
            return this.ws.send(data);
        } else {
            throw new Error('INVALID_STATE_ERR : Pausing to reconnect websocket');
        }
    }

    public close(): boolean {
        if (this.ws) {
            this.forcedClose = true;
            this.ws.close();
            return true;
        }

        return false;
    }
}
