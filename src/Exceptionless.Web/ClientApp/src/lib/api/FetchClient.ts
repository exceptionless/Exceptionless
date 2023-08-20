import { validate as classValidate } from 'class-validator';
import { persisted } from 'svelte-local-storage-store';
import { writable, derived } from 'svelte/store';
import { goto } from '$app/navigation';
import { accessToken } from './Auth';

function createCount() {
	const { subscribe, set, update } = writable(0);

	return {
		subscribe,
		increment: () => update((n) => n + 1),
		decrement: () => update((n) => n - 1),
		reset: () => set(0)
	};
}

const globalRequestCount = createCount();
export const globalLoading = derived(
	globalRequestCount,
	($globalRequestCount) => $globalRequestCount > 0
);
export const base = 'api/v2';

type Fetch = typeof globalThis.fetch;

export type RequestOptions = {
	shouldValidate?: boolean;
	params?: Record<string, unknown>;
	expectedStatusCodes?: number[];
	unauthorizedShouldRedirect?: boolean;
	errorCallback?: (error: Response) => void;
};

export class JsonResponse<T extends object> {
	constructor(response: Response, success: boolean, data?: T | null, problem?: ProblemDetails) {
		this.response = response;
		this.status = response.status;
		this.data = data;
		this.success = success;
		this.problem = Object.assign(new ProblemDetails(), problem);
	}

	success: boolean = false;
	status: number;
	data?: T | null;
	response: Response;
	problem?: ProblemDetails;
}

export class ProblemDetails implements Record<string, unknown> {
	[x: string]: unknown;
	type?: string;
	title?: string;
	status?: number;
	detail?: string;
	instance?: string;
	errors: Record<string, string[] | undefined> = {};

	clear(name: string): ProblemDetails {
		delete this.errors[name];
		return this;
	}

	setErrorMessage(message: string): ProblemDetails {
		this.errors.general = [message];
		return this;
	}
}

export class FetchClient {
	private accessToken: string | null = null;
	private baseUrl = base;

	constructor(
		private fetch: Fetch = window.fetch,
		baseUrl?: string
	) {
		accessToken.subscribe((token) => (this.accessToken = token));
		if (baseUrl) {
			this.baseUrl = baseUrl;
		}
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

	async getJSON<T extends object>(
		url: string,
		options?: RequestOptions
	): Promise<JsonResponse<T>> {
		const response = await this.get(url, options);
		return this.getJSONResponse<T>(response);
	}

	async post(url: string, body?: object | string, options?: RequestOptions): Promise<Response> {
		const problem = await this.validate(body, options);
		if (problem) {
			return this.problemToResponse(problem, url);
		}

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

	async postJSON<T extends object>(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<JsonResponse<T>> {
		const response = await this.post(url, body, options);
		return this.getJSONResponse<T>(response);
	}

	async put(url: string, body?: object | string, options?: RequestOptions): Promise<Response> {
		const problem = await this.validate(body, options);
		if (problem) {
			return this.problemToResponse(problem, url);
		}

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

	async putJSON<T extends object>(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<JsonResponse<T>> {
		const response = await this.put(url, body, options);
		return this.getJSONResponse<T>(response);
	}

	async patch(url: string, body?: object | string, options?: RequestOptions): Promise<Response> {
		const problem = await this.validate(body, options);
		if (problem) {
			return this.problemToResponse(problem, url);
		}

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

	async patchJSON<T extends object>(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<JsonResponse<T>> {
		const response = await this.patch(url, body, options);
		return this.getJSONResponse<T>(response);
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

	private async validate(
		data: unknown,
		options?: RequestOptions
	): Promise<ProblemDetails | null> {
		if (typeof data !== 'object' || (options && options.shouldValidate === false)) return null;

		const validationErrors = await classValidate(data as object);
		if (validationErrors.length === 0) {
			return null;
		}

		const problem = new ProblemDetails();
		for (const ve of validationErrors) {
			problem.errors[ve.property] = Object.values(ve.constraints || {}).map((message) => {
				return `${message.charAt(0).toUpperCase()}${message.slice(1)}.`;
			});
		}

		return problem;
	}

	private problemToResponse(problem: ProblemDetails, url: string): Response {
		const headers = new Headers();
		headers.set('Content-Type', 'application/problem+json');

		return {
			url,
			status: 422,
			statusText: 'Unprocessable Entity',
			body: null,
			bodyUsed: true,
			ok: false,
			headers: headers,
			redirected: false,
			type: 'basic',
			json: async () => problem,
			text: async () => JSON.stringify(problem),
			arrayBuffer: async () => new ArrayBuffer(0),
			blob: async () => new Blob(),
			formData: async () => new FormData(),
			clone: () => {
				throw new Error('Not implemented');
			}
		};
	}

	private async fetchInternal(
		url: string,
		init?: RequestInit,
		options?: RequestOptions
	): Promise<Response> {
		url = this.buildUrl(url, options);

		globalRequestCount.increment();
		this.requestCount.increment();

		if (this.accessToken !== null) {
			if (!init) {
				init = {};
			}

			init.headers = Object.assign(init.headers || {}, {
				Authorization: `Bearer ${this.accessToken}`
			});
		}

		const response = await this.fetch(url, init);

		this.requestCount.decrement();
		globalRequestCount.decrement();

		await this.validateResponse(response, options);

		return response;
	}

	private async getJSONResponse<T extends object>(response: Response): Promise<JsonResponse<T>> {
		const data = await response.json();

		// HACK: https://github.com/dotnet/aspnetcore/issues/39802
		if (
			!response.ok ||
			response.headers.get('Content-Type')?.startsWith('application/problem+json')
		)
			return new JsonResponse<T>(response, response.ok, null, data);

		return new JsonResponse(response, response.ok, data);
	}

	private buildUrl(url: string, options: RequestOptions | undefined): string {
		const isAbsoluteUrl = url.startsWith('http');

		if (url.startsWith('/')) {
			url = url.substring(1);
		}

		if (!url.startsWith('http')) {
			url = this.baseUrl + '/' + url;
		}

		const parsed = new URL(url, window.location.origin);

		if (options?.params) {
			for (const [key, value] of Object.entries(options?.params)) {
				parsed.searchParams.append(key, value as string);
			}

			url = parsed.toString();
		}

		return isAbsoluteUrl ? url : `${parsed.pathname}${parsed.search}`;
	}

	private async validateResponse(response: Response, options: RequestOptions | undefined) {
		if (response.ok) {
			return;
		}

		if (options?.expectedStatusCodes && options.expectedStatusCodes.includes(response.status)) {
			return;
		}

		if (response.status === 401 && options?.unauthorizedShouldRedirect != false) {
			const returnUrl = location.href;
			await goto(`/login?url=${returnUrl}`, { replaceState: true });
			return;
		}

		if (options?.errorCallback) {
			options.errorCallback(response);
		} else {
			throw response;
		}
	}
}
