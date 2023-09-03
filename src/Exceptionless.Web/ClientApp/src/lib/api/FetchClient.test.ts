import { describe, expect, it } from 'vitest';
import { FetchClient } from './FetchClient';
import { validate } from '$lib/validation/validation';
import { Login } from '$lib/models/api.generated';

describe('fetch client', () => {
	it('can getJSON with middleware', async () => {
		const fakeFetch = (): Promise<Response> =>
			new Promise((resolve) => {
				console.log('fake fetch');
				const data = JSON.stringify({
					userId: 1,
					id: 1,
					title: 'delectus aut autem',
					completed: false
				});
				resolve(new Response(data));
			});
		const client = new FetchClient(fakeFetch);
		let called = false;
		client.use(async (ctx, next) => {
			expect(ctx).not.toBeNull();
			expect(ctx.request).not.toBeNull();
			expect(ctx.response).toBeNull();
			called = true;
			await next();
			expect(ctx.response).not.toBeNull();
		});
		expect(client).not.toBeNull();

		type Todo = { userId: number; id: number; title: string; completed: boolean };
		const r = await client.getJSON<Todo>('https://jsonplaceholder.typicode.com/todos/1');
		expect(r.ok).toBe(true);
		expect(r.status).toBe(200);
		expect(r.data).not.toBeNull();
		expect(called).toBe(true);
		expect(r.data!.userId).toBe(1);
		expect(r.data!.id).toBe(1);
		expect(r.data!.title).toBe('delectus aut autem');
		expect(r.data!.completed).toBe(false);
	});

	it('can postJSON with middleware', async () => {
		const fakeFetch = (): Promise<Response> =>
			new Promise((resolve) => {
				console.log('fake fetch');
				const data = JSON.stringify({
					userId: 1,
					id: 1,
					title: 'delectus aut autem',
					completed: false
				});
				resolve(new Response(data));
			});
		const client = new FetchClient(fakeFetch);
		let called = false;
		client.use(async (ctx, next) => {
			expect(ctx).not.toBeNull();
			expect(ctx.request).not.toBeNull();
			expect(ctx.response).toBeNull();
			called = true;
			await next();
			expect(ctx.response).not.toBeNull();
		});
		expect(client).not.toBeNull();

		type Todo = { userId: number; id: number; title: string; completed: boolean };
		const r = await client.postJSON<Todo>('https://jsonplaceholder.typicode.com/todos/1');
		expect(r.ok).toBe(true);
		expect(r.status).toBe(200);
		expect(r.data).not.toBeNull();
		expect(called).toBe(true);
		expect(r.data!.userId).toBe(1);
		expect(r.data!.id).toBe(1);
		expect(r.data!.title).toBe('delectus aut autem');
		expect(r.data!.completed).toBe(false);
	});

	it('can putJSON with middleware', async () => {
		const fakeFetch = (): Promise<Response> =>
			new Promise((resolve) => {
				console.log('fake fetch');
				const data = JSON.stringify({
					userId: 1,
					id: 1,
					title: 'delectus aut autem',
					completed: false
				});
				resolve(new Response(data));
			});
		const client = new FetchClient(fakeFetch);
		let called = false;
		client.use(async (ctx, next) => {
			expect(ctx).not.toBeNull();
			expect(ctx.request).not.toBeNull();
			expect(ctx.response).toBeNull();
			called = true;
			await next();
			expect(ctx.response).not.toBeNull();
		});
		expect(client).not.toBeNull();

		type Todo = { userId: number; id: number; title: string; completed: boolean };
		const r = await client.putJSON<Todo>('https://jsonplaceholder.typicode.com/todos/1');
		expect(r.ok).toBe(true);
		expect(r.status).toBe(200);
		expect(r.data).not.toBeNull();
		expect(called).toBe(true);
		expect(r.data!.userId).toBe(1);
		expect(r.data!.id).toBe(1);
		expect(r.data!.title).toBe('delectus aut autem');
		expect(r.data!.completed).toBe(false);
	});

	it('can delete with middleware', async () => {
		const fakeFetch = (): Promise<Response> =>
			new Promise((resolve) => {
				console.log('fake fetch');
				resolve(new Response());
			});
		const client = new FetchClient(fakeFetch);
		let called = false;
		client.use(async (ctx, next) => {
			expect(ctx).not.toBeNull();
			expect(ctx.request).not.toBeNull();
			expect(ctx.response).toBeNull();
			called = true;
			await next();
			expect(ctx.response).not.toBeNull();
		});
		expect(client).not.toBeNull();

		const r = await client.delete('https://jsonplaceholder.typicode.com/todos/1');
		expect(r.ok).toBe(true);
		expect(r.status).toBe(200);
		expect(called).toBe(true);
	});

	it('can abort getJSON', async () => {
		const controller = new AbortController();
		const fakeFetch = (r: unknown): Promise<Response> =>
			new Promise((resolve) => {
				const request = r as Request;
				console.log('fake fetch');
				const data = JSON.stringify({
					userId: 1,
					id: 1,
					title: 'delectus aut autem',
					completed: false
				});
				const responseTimeout = setTimeout(function () {
					resolve(new Response(data));
				}, 5000);
				request.signal.addEventListener('abort', () => {
					clearTimeout(responseTimeout);
					resolve(
						new Response(null, { status: 0, statusText: 'The user aborted a request.' })
					);
				});
			});
		const client = new FetchClient(fakeFetch);
		client
			.getJSON('https://jsonplaceholder.typicode.com/todos/1', { signal: controller.signal })
			.then((r) => {
				expect(r).rejects.toThrow('The user aborted a request.');
			});
		controller.abort();
	});

	it('will validate postJSON model', async () => {
		let called = false;
		const fakeFetch = (): Promise<Response> =>
			new Promise((resolve) => {
				called = true;
				const data = JSON.stringify({
					userId: 1,
					id: 1,
					title: 'delectus aut autem',
					completed: false
				});
				resolve(new Response(data));
			});

		const client = new FetchClient(fakeFetch);
		const data = new Login();
		data.email = 'test@test';
		data.password = 'test';
		const response = await client.postJSON(
			'https://jsonplaceholder.typicode.com/todos/1',
			data,
			{ modelValidator: validate }
		);
		expect(response.ok).toBe(false);
		expect(called).toBe(false);
		expect(response.status).toBe(422);
		expect(response.data).toBeNull();
		expect(response.problem).not.toBeNull();
		expect(response.problem!.errors).not.toBeNull();
		expect(response.problem!.errors.password).not.toBeNull();
		expect(response.problem!.errors.password!.length).toBe(1);
		expect(response.problem!.errors.password![0]).toBe(
			'Password must be longer than or equal to 6 characters.'
		);
	});
});
