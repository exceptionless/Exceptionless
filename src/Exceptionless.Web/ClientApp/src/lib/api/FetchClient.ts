import { type Readable, get, writable, readable, derived } from 'svelte/store';
import { type Links, parseLinkHeader, type Link } from '@web3-storage/parse-link-header';

const defaultBaseUrl = 'api/v2';

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
	shouldValidateModel?: boolean;
	modelValidator?: (model: object | null) => Promise<ProblemDetails | null>;
	params?: Record<string, unknown>;
	expectedStatusCodes?: number[];
	headers?: Record<string, string>;
	errorCallback?: (error: Response) => void;
	middleware?: FetchClientMiddleware[];
	signal?: AbortSignal;
};

type FetchClientResponse<T> = Response & {
	data: T | null;
	problem: ProblemDetails;
	links: Links & { next?: Link; previous?: Link };
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
	private baseUrl = defaultBaseUrl;
	private fetch!: Fetch;
	private middleware: FetchClientMiddleware[] = [];

	constructor(fetch?: Fetch, baseUrl?: string) {
		if (fetch) {
			this.fetch = fetch;
		} else {
			this.fetch = globalThis.fetch;
		}
		if (baseUrl) {
			this.baseUrl = baseUrl;
		}
	}

	requestCount = createCount();
	loading = derived(this.requestCount, ($requestCount) => $requestCount > 0);

	public use(...mw: FetchClientMiddleware[]): void {
		this.middleware.push(...mw);
	}

	async get(url: string, options?: RequestOptions): Promise<FetchClientResponse<unknown>> {
		const response = await this.fetchInternal(
			url,
			{
				method: 'GET',
				headers: Object.assign(
					{ 'Content-Type': 'application/json' },
					options?.headers ?? {}
				)
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
		const problem = await this.validate(body, options);
		if (problem) return this.problemToResponse(problem, url);

		const response = await this.fetchInternal(
			url,
			{
				method: 'POST',
				headers: Object.assign(
					{ 'Content-Type': 'application/json' },
					options?.headers ?? {}
				),
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
		const problem = await this.validate(body, options);
		if (problem) return this.problemToResponse(problem, url);

		const response = await this.fetchInternal(
			url,
			{
				method: 'PUT',
				headers: Object.assign(
					{ 'Content-Type': 'application/json' },
					options?.headers ?? {}
				),
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
		const problem = await this.validate(body, options);
		if (problem) return this.problemToResponse(problem, url);

		const response = await this.fetchInternal(
			url,
			{
				method: 'PATCH',
				headers: Object.assign(
					{ 'Content-Type': 'application/json' },
					options?.headers ?? {}
				),
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

		if (options?.modelValidator === undefined) return null;

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
		if (accessToken !== null)
			init = Object.assign({}, init, { headers: { Authorization: `Bearer ${accessToken}` } });
		if (options?.signal) init = Object.assign({}, init, { signal: options.signal });

		const fetchMiddleware = async (ctx: FetchClientContext, next: Next) => {
			const response = await this.fetch(ctx.request);
			if (ctx.request.headers.get('Content-Type') === 'application/json')
				ctx.response = await this.getJSONResponse<T>(response);
			else
				ctx.response = Object.assign(response, {
					data: null,
					problem: new ProblemDetails(),
					links: {}
				}) as FetchClientResponse<T>;

			ctx.response.links = parseLinkHeader(response.headers.get('Link')) || {};
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

		await this.validateResponse(context.response, options);

		return context.response as FetchClientResponse<T>;
	}

	private async invokeMiddleware(
		context: FetchClientContext,
		middleware: FetchClientMiddleware[]
	): Promise<void> {
		if (!middleware.length) return;

		const mw = middleware[0];

		return mw(context, async () => {
			await this.invokeMiddleware(context, middleware.slice(1));
		});
	}

	private async getJSONResponse<T>(response: Response): Promise<FetchClientResponse<T>> {
		const data = await response.json();
		const jsonResponse = response as FetchClientResponse<T>;

		// HACK: https://github.com/dotnet/aspnetcore/issues/39802
		if (
			!response.ok ||
			response.headers.get('Content-Type')?.startsWith('application/problem+json')
		) {
			jsonResponse.problem = data as ProblemDetails;
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
			links: {},
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

	private buildUrl(url: string, options: RequestOptions | undefined): string {
		const isAbsoluteUrl = url.startsWith('http');

		if (url.startsWith('/')) {
			url = url.substring(1);
		}

		if (!url.startsWith('http')) {
			url = this.baseUrl + '/' + url;
		}

		const origin = isAbsoluteUrl ? undefined : window.location.origin ?? '';

		const parsed = new URL(url, origin);

		if (options?.params) {
			for (const [key, value] of Object.entries(options?.params)) {
				parsed.searchParams.append(key, value as string);
			}

			url = parsed.toString();
		}

		return isAbsoluteUrl ? url : `${parsed.pathname}${parsed.search}`;
	}

	private async validateResponse(response: Response | null, options: RequestOptions | undefined) {
		if (!response) throw new Error('Response is null');

		if (response.ok) return;

		if (options?.expectedStatusCodes && options.expectedStatusCodes.includes(response.status))
			return;

		if (options?.errorCallback) {
			options.errorCallback(response);
		} else {
			throw response;
		}
	}
}

export const globalFetchClient = new FetchClient();
