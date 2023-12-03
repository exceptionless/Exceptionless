import { derived, get, type Readable, readable, writable } from 'svelte/store';
import { type Link, type Links, parseLinkHeader } from '@web3-storage/parse-link-header';

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

let accessTokenStore = readable<string | null>(null);
export function setAccessTokenStore(accessToken: Readable<string | null>) {
	accessTokenStore = accessToken;
}

type Fetch = typeof globalThis.fetch;
type Next = () => Promise<void>;
type FetchClientContext = {
	request: Request;
	response: FetchClientResponse<unknown> | null;
	data: Record<string, unknown>;
};
export type FetchClientMiddleware = (context: FetchClientContext, next: Next) => Promise<void>;

export type RequestOptions = {
	baseUrl?: string;
	shouldValidateModel?: boolean;
	modelValidator?: (model: object | null) => Promise<ProblemDetails | null>;
	params?: Record<string, unknown>;
	expectedStatusCodes?: number[];
	headers?: Record<string, string>;
	errorCallback?: (error: Response) => void;
	middleware?: FetchClientMiddleware[];
	signal?: AbortSignal;
};

let defaultOptions: RequestOptions = {};

export function setDefaultRequestOptions(options: RequestOptions) {
	defaultOptions = { ...defaultOptions, ...options };
}

export function setDefaultModelValidator(
	validate: (model: object | null) => Promise<ProblemDetails | null>
) {
	defaultOptions = { ...defaultOptions, modelValidator: validate };
}

export function setDefaultBaseUrl(url: string) {
	defaultOptions = { ...defaultOptions, baseUrl: url };
}

export function useGlobalMiddleware(middleware: FetchClientMiddleware) {
	defaultOptions = {
		...defaultOptions,
		middleware: [...(defaultOptions.middleware ?? []), middleware]
	};
}

export type FetchClientResponse<T> = Response & {
	data: T | null;
	problem: ProblemDetails;
	meta: Record<string, unknown> & { links: Links & { next?: Link; previous?: Link } };
};

export class ProblemDetails implements Record<string, unknown> {
	[key: string]: unknown;
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
	private fetch!: Fetch;
	private middleware: FetchClientMiddleware[] = [];

	constructor(fetch?: Fetch) {
		if (fetch) {
			this.fetch = fetch;
		} else {
			this.fetch = globalThis.fetch;
		}
	}

	requestCount = createCount();
	loading = derived(this.requestCount, ($requestCount) => $requestCount > 0);

	public use(...mw: FetchClientMiddleware[]): void {
		this.middleware.push(...mw);
	}

	async get(url: string, options?: RequestOptions): Promise<FetchClientResponse<unknown>> {
		options = { ...defaultOptions, ...options };
		const response = await this.fetchInternal(
			url,
			{
				method: 'GET',
				headers: {
					...{ 'Content-Type': 'application/json' },
					...options?.headers
				}
			},
			options
		);

		return response;
	}

	getJSON<T>(url: string, options?: RequestOptions): Promise<FetchClientResponse<T>> {
		return this.get(url, options) as Promise<FetchClientResponse<T>>;
	}

	async post(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<FetchClientResponse<unknown>> {
		options = { ...defaultOptions, ...options };
		const problem = await this.validate(body, options);
		if (problem) return this.problemToResponse(problem, url);

		const response = await this.fetchInternal(
			url,
			{
				method: 'POST',
				headers: { 'Content-Type': 'application/json', ...options?.headers },
				body: typeof body === 'string' ? body : JSON.stringify(body)
			},
			options
		);

		return response;
	}

	postJSON<T>(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<FetchClientResponse<T>> {
		return this.post(url, body, options) as Promise<FetchClientResponse<T>>;
	}

	async put(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<FetchClientResponse<unknown>> {
		options = { ...defaultOptions, ...options };
		const problem = await this.validate(body, options);
		if (problem) return this.problemToResponse(problem, url);

		const response = await this.fetchInternal(
			url,
			{
				method: 'PUT',
				headers: { 'Content-Type': 'application/json', ...options?.headers },
				body: typeof body === 'string' ? body : JSON.stringify(body)
			},
			options
		);

		return response;
	}

	putJSON<T>(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<FetchClientResponse<T>> {
		return this.put(url, body, options) as Promise<FetchClientResponse<T>>;
	}

	async patch(url: string, body?: object | string, options?: RequestOptions): Promise<Response> {
		options = { ...defaultOptions, ...options };
		const problem = await this.validate(body, options);
		if (problem) return this.problemToResponse(problem, url);

		const response = await this.fetchInternal(
			url,
			{
				method: 'PATCH',
				headers: { 'Content-Type': 'application/json', ...options?.headers },
				body: typeof body === 'string' ? body : JSON.stringify(body)
			},
			options
		);

		return response;
	}

	patchJSON<T>(
		url: string,
		body?: object | string,
		options?: RequestOptions
	): Promise<FetchClientResponse<T>> {
		return this.patch(url, body, options) as Promise<FetchClientResponse<T>>;
	}

	async delete(url: string, options?: RequestOptions): Promise<FetchClientResponse<unknown>> {
		options = { ...defaultOptions, ...options };
		return await this.fetchInternal(
			url,
			{
				method: 'DELETE',
				headers: options?.headers ?? {}
			},
			options
		);
	}

	private async validate(
		data: unknown,
		options?: RequestOptions
	): Promise<ProblemDetails | null> {
		if (typeof data !== 'object' || (options && options.shouldValidateModel === false))
			return null;

		if (options?.modelValidator === undefined) {
			return null;
		}

		const problem = await options.modelValidator(data as object);
		if (!problem) return null;

		return problem;
	}

	private async fetchInternal<T>(
		url: string,
		init?: RequestInit,
		options?: RequestOptions
	): Promise<FetchClientResponse<T>> {
		url = this.buildUrl(url, options);

		const accessToken = get(accessTokenStore);
		if (accessToken !== null) {
			init = {
				...init,
				...{
					headers: { ...init?.headers, Authorization: `Bearer ${accessToken}` }
				}
			};
		}

		if (options?.signal) {
			init = { ...init, signal: options.signal };
		}

		const fetchMiddleware = async (ctx: FetchClientContext, next: Next) => {
			const response = await this.fetch(ctx.request);
			if (
				ctx.request.headers.get('Content-Type')?.startsWith('application/json') ||
				response?.headers.get('Content-Type')?.startsWith('application/problem+json')
			) {
				ctx.response = await this.getJSONResponse<T>(response);
			} else {
				ctx.response = response as FetchClientResponse<T>;
				ctx.response.data = null;
				ctx.response.problem = new ProblemDetails();
			}

			ctx.response.meta = { links: parseLinkHeader(response.headers.get('Link')) || {} };

			await next();
		};
		const middleware = [...this.middleware, ...(options?.middleware ?? []), fetchMiddleware];

		globalRequestCount.increment();
		this.requestCount.increment();

		const context: FetchClientContext = {
			request: new Request(url, init),
			response: null,
			data: {}
		};
		await this.invokeMiddleware(context, middleware);

		this.requestCount.decrement();
		globalRequestCount.decrement();

		this.validateResponse(context.response, options);

		return context.response as FetchClientResponse<T>;
	}

	private async invokeMiddleware(
		context: FetchClientContext,
		middleware: FetchClientMiddleware[]
	): Promise<void> {
		if (!middleware.length) return;

		const mw = middleware[0];

		return await mw(context, async () => {
			await this.invokeMiddleware(context, middleware.slice(1));
		});
	}

	private async getJSONResponse<T>(response: Response): Promise<FetchClientResponse<T>> {
		let data = null;
		try {
			data = await response.json();
		} catch {
			data = new ProblemDetails();
			data.setErrorMessage('Unable to deserialize response data');
		}

		const jsonResponse = response as FetchClientResponse<T>;

		if (
			!response.ok ||
			response.headers.get('Content-Type')?.startsWith('application/problem+json')
		) {
			jsonResponse.problem = new ProblemDetails();
			Object.assign(jsonResponse.problem, data);
			jsonResponse.data = null;
			return jsonResponse;
		}

		jsonResponse.problem = new ProblemDetails();
		jsonResponse.data = data;

		return jsonResponse;
	}

	private problemToResponse(problem: ProblemDetails, url: string): FetchClientResponse<unknown> {
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
			problem: problem,
			data: null,
			meta: { links: {} },
			type: 'basic',
			json: () => new Promise((resolve) => resolve(problem)),
			text: () => new Promise((resolve) => resolve(JSON.stringify(problem))),
			arrayBuffer: () => new Promise((resolve) => resolve(new ArrayBuffer(0))),
			blob: () => new Promise((resolve) => resolve(new Blob())),
			formData: () => new Promise((resolve) => resolve(new FormData())),
			clone: () => {
				throw new Error('Not implemented');
			}
		};
	}

	private buildUrl(url: string, options: RequestOptions | undefined): string {
		const isAbsoluteUrl = url.startsWith('http');

		if (url.startsWith('/')) {
			url = url.substring(1);
		}

		if (!url.startsWith('http') && options?.baseUrl) {
			url = options.baseUrl + '/' + url;
		}

		const origin = isAbsoluteUrl ? undefined : window.location.origin ?? '';
		const parsed = new URL(url, origin);

		if (options?.params) {
			for (const [key, value] of Object.entries(options?.params)) {
				if (value !== undefined && value !== null) {
					parsed.searchParams.append(key, value as string);
				}
			}

			url = parsed.toString();
		}

		return isAbsoluteUrl ? url : `${parsed.pathname}${parsed.search}`;
	}

	private validateResponse(response: Response | null, options: RequestOptions | undefined) {
		if (!response) {
			throw new Error('Response is null');
		}

		if (response.ok) {
			return;
		}

		if (options?.expectedStatusCodes && options.expectedStatusCodes.includes(response.status)) {
			return;
		}

		if (options?.errorCallback) {
			options.errorCallback(response);
		}
		throw response;
	}
}

export const globalFetchClient = new FetchClient();
