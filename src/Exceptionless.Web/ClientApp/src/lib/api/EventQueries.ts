import { createInfiniteQuery, createQuery } from '@tanstack/svelte-query';
import { FetchClient, JsonResponse, ProblemDetails } from './FetchClient';
import type { PersistentEvent, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';

const queryKey: string = 'PersistentEvent';

export type GetEventsMode =
	| 'summary'
	| 'stack_recent'
	| 'stack_frequent'
	| 'stack_new'
	| 'stack_users'
	| null;

export interface IGetEventsParams {
	filter?: string;
	sort?: string;
	time?: string;
	offset?: string;
	mode?: GetEventsMode;
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

export function useGetEventSummariesQuery(params?: IGetEventsParams) {
	return createQuery<
		JsonResponse<SummaryModel<SummaryTemplateKeys>[]>,
		ProblemDetails,
		JsonResponse<SummaryModel<SummaryTemplateKeys>[]>
	>(
		[queryKey, ...Object.entries({ mode: 'summary', ...params }).map((kvp) => kvp.join('='))],
		async () => {
			const api = new FetchClient();
			const response = await api.getJSON<SummaryModel<SummaryTemplateKeys>[]>('events', {
				params: { mode: 'summary', ...params }
			});

			if (response.success) {
				return response;
			}

			throw response.problem;
		}
	);
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
