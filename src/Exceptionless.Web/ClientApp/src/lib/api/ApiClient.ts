import { writable, derived } from 'svelte/store';
import { persisted } from 'svelte-local-storage-store';

function createCount() {
	const { subscribe, set, update } = writable(0);

	return {
		subscribe,
		increment: () => update((n) => n + 1),
		decrement: () => update((n) => n - 1),
		reset: () => set(0)
	};
}

export const bearerToken = persisted<string | null>('bearer-token', null);

type Fetch = typeof globalThis.fetch;

export type RequestOptions = {
	params?: Record<string, unknown>;
	expectedStatusCodes?: number[];
};

export type UnauthorizedAction = () => void;

export class ApiClient {
	private bearerToken: string | null = null;
	private unauthorizedAction?: UnauthorizedAction;

	constructor(private fetch: Fetch = window.fetch) {
		bearerToken.subscribe((token) => (this.bearerToken = token));
	}

	public setUnauthorizedAction(action: UnauthorizedAction) {
		this.unauthorizedAction = action;
	}

	requestCount = createCount();
	loading = derived(this.requestCount, ($requestCount) => $requestCount > 0);

	async get(url: string, options?: RequestOptions): Promise<Response> {
		const response = await this.fetchInternal(
			url,
			{
				method: 'GET',
				headers: {
					'Content-Type': 'application/json'
				}
			},
			options
		);

		return response;
	}

	async getJSON<T>(url: string, options?: RequestOptions): Promise<T> {
		const response = await this.get(url, options);
		const data = await response.json();
		return data as T;
	}

	async post(url: string, body?: object | string, options?: RequestOptions): Promise<Response> {
		const response = await this.fetchInternal(
			url,
			{
				method: 'POST',
				headers: {
					'Content-Type': 'application/json'
				},
				body: typeof body === 'string' ? body : JSON.stringify(body)
			},
			options
		);

		return response;
	}

	async postJSON<T extends object = Response>(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<T> {
		const response = await this.post(url, body, options);
		const data = await response.json();
		return data as T;
	}

	async put(url: string, body?: object | string, options?: RequestOptions): Promise<Response> {
		const response = await this.fetchInternal(
			url,
			{
				method: 'PUT',
				headers: {
					'Content-Type': 'application/json'
				},
				body: typeof body === 'string' ? body : JSON.stringify(body)
			},
			options
		);

		return response;
	}

	async putJSON<T = object>(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<T> {
		const response = await this.put(url, body, options);
		const data = await response.json();
		return data as T;
	}

	async patch(url: string, body?: object | string, options?: RequestOptions): Promise<Response> {
		const response = await this.fetchInternal(
			url,
			{
				method: 'PATCH',
				headers: {
					'Content-Type': 'application/json'
				},
				body: typeof body === 'string' ? body : JSON.stringify(body)
			},
			options
		);

		return response;
	}

	async patchJSON<T>(url: string, body?: object | string, options?: RequestOptions): Promise<T> {
		const response = await this.patch(url, body, options);
		const data = await response.json();
		return data as T;
	}

	async delete(url: string, options?: RequestOptions): Promise<Response> {
		const response = await this.fetchInternal(
			url,
			{
				method: 'DELETE',
				headers: {
					'Content-Type': 'application/json'
				}
			},
			options
		);

		return response;
	}

	private async fetchInternal(
		url: string,
		init?: RequestInit,
		options?: RequestOptions
	): Promise<Response> {
		url = this.buildUrl(url, options);

		this.requestCount.increment();

		if (this.bearerToken) {
			if (!init) {
				init = {};
			}

			if (!init.headers) {
				init.headers = new Headers();
			}

			const headers = init.headers as Headers;
			headers.set('Authorization', `Bearer ${this.bearerToken}`);
		}

		const response = await this.fetch(url, init);

		this.requestCount.decrement();

		this.validateResponse(response, options);

		return response;
	}

	private buildUrl(url: string, options?: RequestOptions): string {
		const isAbsoluteUrl = url.startsWith('http');
		const parsed = new URL(url, window.location.origin);
		if (options?.params) {
			for (const [key, value] of Object.entries(options?.params)) {
				parsed.searchParams.append(key, value as string);
			}

			url = parsed.toString();
		}

		return isAbsoluteUrl ? url : `${parsed.pathname}${parsed.search}`;
	}

	private validateResponse(response: Response, options?: RequestOptions) {
		if (response.ok) {
			return;
		}

		if (response.status === 401 && this.unauthorizedAction) {
			this.unauthorizedAction();
			return;
		}

		if (options?.expectedStatusCodes && options.expectedStatusCodes.includes(response.status)) {
			return;
		}

		throw response;
	}
}

export const client = new ApiClient();
