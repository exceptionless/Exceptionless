import { accessToken } from './auth';

export class WebSocketClient {
	private accessToken: string | null = null;

	public reconnectInterval: number = 1000;
	public timeoutInterval: number = 2000;
	public readyState: number = WebSocket.CLOSED;
	public url: string;

	private forcedClose: boolean = false;
	private timedOut: boolean = false;
	private ws: WebSocket | null = null;

	public onConnecting: () => void = () => {};
	public onOpen: (ev: Event) => void = () => {};
	public onMessage: (ev: MessageEvent) => void = () => {};
	public onError: (ev: Event) => void = () => {};
	public onClose: (ev: CloseEvent) => void = () => {};

	constructor(path: string = '/api/v2/push') {
		const { host, protocol } = window.location;
		const wsProtocol = protocol === 'https:' ? 'wss://' : 'ws://';
		this.url = `${wsProtocol}${host}${path}`;

		accessToken.subscribe((token) => {
			this.accessToken = token;
			this.close();

			if (this.accessToken) {
				this.connect();
			}
		});
	}

	public connect(reconnectAttempt: boolean = true) {
		// Reset state
		this.readyState = WebSocket.CONNECTING;
		this.forcedClose = false;

		this.ws = new WebSocket(`${this.url}?access_token=${this.accessToken}`);
		this.onConnecting();

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
			this.onOpen(event);
		};

		this.ws.onclose = (event: CloseEvent) => {
			clearTimeout(timeout);
			this.ws = null;

			if (this.forcedClose) {
				this.readyState = WebSocket.CLOSED;
				this.onClose(event);
			} else {
				this.readyState = WebSocket.CONNECTING;
				this.onConnecting();
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
