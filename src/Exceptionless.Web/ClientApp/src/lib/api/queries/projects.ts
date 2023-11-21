import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';
import { derived, readable, type Readable } from 'svelte/store';
import type { ViewProject } from '$lib/models/api';
import { FetchClient, type FetchClientResponse, type ProblemDetails } from '$api/FetchClient';

export const queryKey: string = 'Project';

export function getProjectByIdQuery(id: string | Readable<string | null>) {
	const readableId = typeof id === 'string' ? readable(id) : id;
	return createQuery<ViewProject, ProblemDetails>(
		derived(readableId, ($id) => ({
			enabled: !!$id,
			queryKey: [queryKey, $id],
			queryFn: async ({ signal }: { signal: AbortSignal }) => {
				const api = new FetchClient();
				const response = await api.getJSON<ViewProject>(`projects/${$id}`, {
					signal
				});

				if (response.ok) {
					return response.data!;
				}

				throw response.problem;
			}
		}))
	);
}

export function mutatePromoteTab(id: string) {
	const client = useQueryClient();
	return createMutation<FetchClientResponse<unknown>, ProblemDetails, { name: string }>({
		mutationKey: [queryKey, id],
		mutationFn: async ({ name: string }) => {
			const api = new FetchClient();
			const response = await api.post(`projects/${id}/promotedtabs`, undefined, {
				params: { name }
			});

			if (response.ok) {
				return response;
			}

			throw response.problem;
		},
		onSettled: () => {
			client.invalidateQueries({ queryKey: [queryKey, id] });
		}
	});
}

export function mutateDemoteTab(id: string) {
	const client = useQueryClient();
	return createMutation<FetchClientResponse<unknown>, ProblemDetails, { name: string }>({
		mutationKey: [queryKey, id],
		mutationFn: async ({ name: string }) => {
			const api = new FetchClient();
			const response = await api.delete(`projects/${id}/promotedtabs`, {
				params: { name }
			});

			if (response.ok) {
				return response;
			}

			throw response.problem;
		},
		onSettled: () => {
			client.invalidateQueries({ queryKey: [queryKey, id] });
		}
	});
}
