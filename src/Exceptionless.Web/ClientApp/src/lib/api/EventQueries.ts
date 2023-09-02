import { createInfiniteQuery, createQuery } from '@tanstack/svelte-query';
import { FetchClient, JsonResponse, ProblemDetails } from './FetchClient';
import type { PersistentEvent } from '$lib/models/api';

const queryKey: string = 'PersistentEvent';

export interface IGetEventsParams {
	filter?: string;
	sort?: string;
	time?: string;
	offset?: string;
	mode?: 'summary' | null;
	page?: number;
	limit?: number;
	before?: string;
	after?: string;
}

export function useGetEventsQuery(params?: IGetEventsParams) {
	return createQuery<
		JsonResponse<PersistentEvent[]>,
		ProblemDetails,
		JsonResponse<PersistentEvent[]>
	>([queryKey, ...Object.entries({ ...params }).map((kvp) => kvp.join('='))], async () => {
		const api = new FetchClient();
		const response = await api.getJSON<PersistentEvent[]>('events', {
			params: { ...params }
		});

		if (response.success) {
			return response;
		}

		throw response.problem;
	});
}

export function useGetEventsInfiniteQuery(params?: IGetEventsParams) {
	return createInfiniteQuery<JsonResponse<PersistentEvent[]>, ProblemDetails>(
		[queryKey],
		async ({ pageParam }) => {
			const api = new FetchClient();
			const mergedParams = { ...params, ...pageParam };
			return await api.getJSON<PersistentEvent[]>('events', { params: mergedParams });
		},
		{
			getPreviousPageParam: (firstPage) => firstPage.nextPageParams,
			getNextPageParam: (lastPage) => lastPage.nextPageParams
		}
	);
}
