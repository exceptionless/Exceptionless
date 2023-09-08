import { createInfiniteQuery, createQuery } from '@tanstack/svelte-query';
import { FetchClient, type FetchClientResponse, ProblemDetails } from './FetchClient';
import type { PersistentEvent, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
import { getQueryParametersFromLink } from './Link';

export const queryKey: string = 'PersistentEvent';

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
		FetchClientResponse<PersistentEvent[]>,
		ProblemDetails,
		FetchClientResponse<PersistentEvent[]>
	>(
		[queryKey, ...Object.entries({ ...params }).map((kvp) => kvp.join('='))],
		async ({ signal }) => {
			const api = new FetchClient();
			const response = await api.getJSON<PersistentEvent[]>('events', {
				params: { ...params },
				signal
			});

			if (response.ok) {
				return response;
			}

			throw response.problem;
		},
		{
			keepPreviousData: true
		}
	);
}

export function useGetEventSummariesQuery(params?: IGetEventsParams) {
	return createQuery<
		FetchClientResponse<SummaryModel<SummaryTemplateKeys>[]>,
		ProblemDetails,
		FetchClientResponse<SummaryModel<SummaryTemplateKeys>[]>
	>(
		[queryKey, ...Object.entries({ mode: 'summary', ...params }).map((kvp) => kvp.join('='))],
		async ({ signal }) => {
			const api = new FetchClient();
			const response = await api.getJSON<SummaryModel<SummaryTemplateKeys>[]>('events', {
				params: { mode: 'summary', ...params },
				signal
			});

			if (response.ok) {
				return response;
			}

			throw response.problem;
		},
		{
			keepPreviousData: true
		}
	);
}

export function useGetEventSummariesInfiniteQuery(params?: IGetEventsParams) {
	return createInfiniteQuery<
		FetchClientResponse<SummaryModel<SummaryTemplateKeys>[]>,
		ProblemDetails
	>(
		[queryKey],
		async ({ pageParam, signal }) => {
			const api = new FetchClient();
			const mergedParams = { ...params, ...pageParam };
			return await api.getJSON<SummaryModel<SummaryTemplateKeys>[]>('events', {
				params: mergedParams,
				signal
			});
		},
		{
			keepPreviousData: true,
			getPreviousPageParam: (firstPage) => getQueryParametersFromLink(firstPage.links.next),
			getNextPageParam: (lastPage) => getQueryParametersFromLink(lastPage.links.previous),
			select: (data) => ({
				pages: [...data.pages].reverse(),
				pageParams: [...data.pageParams].reverse()
			})
		}
	);
}
