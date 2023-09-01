<script lang="ts">
	import { FetchClient, ProblemDetails } from '$lib/api/FetchClient';
	import type { PersistentEvent } from '$lib/models/api.generated';
	import { useQuery } from '@sveltestack/svelte-query';

	const api = new FetchClient();
	const eventsQueryResult = useQuery<PersistentEvent[], ProblemDetails, PersistentEvent[]>(
		['PersistentEvent'],
		async () => {
			const response = await api.getJSON<PersistentEvent[]>('events', {
				params: { mode: 'summary' }
			});
			if (response.success) {
				return response.data ?? [];
			}

			throw response.problem;
		}
	);
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<div class="stats shadow">
	<div class="stat place-items-center">
		<div class="stat-title">Events</div>
		<div class="stat-value">31K</div>
		<div class="stat-desc">Jan 1st - Feb 1st</div>
	</div>

	<div class="stat place-items-center">
		<div class="stat-title">Stacks</div>
		<div class="stat-value">4,200</div>
		<div class="stat-desc">↗︎ 400 (22%)</div>
	</div>

	<div class="stat place-items-center">
		<div class="stat-title">New Stacks</div>
		<div class="stat-value">1,200</div>
		<div class="stat-desc">↘︎ 90 (14%)</div>
	</div>

	<div class="stat place-items-center">
		<div class="stat-title">Events Per Hour</div>
		<div class="stat-value">1,200</div>
		<div class="stat-desc">↘︎ 90 (14%)</div>
	</div>
</div>

<h1 class="text-xl mt-5">Events</h1>

<div>
	<table class="table">
		<!-- head -->
		<thead>
			<tr>
				<th>
					<label>
						<input type="checkbox" class="checkbox" />
					</label>
				</th>
				<th>Summary</th>
				<th>User</th>
				<th>Date</th>
			</tr>
		</thead>
		<tbody>
			{#if $eventsQueryResult.isSuccess}
				{#each $eventsQueryResult.data as ev}
					<tr>
						<th>
							<label>
								<input type="checkbox" class="checkbox" />
							</label>
						</th>
						<td>{ev.data?.Message}</td>
						<td>{ev.data?.Identity ?? ev.data?.Name}</td>
						<td>{ev.date}</td>
					</tr>
				{/each}
			{/if}
		</tbody>
		<tfoot>
			<tr>
				<th>
					<div class="dropdown">
						<!-- svelte-ignore a11y-no-noninteractive-tabindex -->
						<div tabindex="0" class="btn btn-sm">Bulk Action</div>
						<!-- svelte-ignore a11y-no-noninteractive-tabindex -->
						<ul
							tabindex="0"
							class="menu dropdown-content rounded-box z-[1] w-52 bg-base-100 p-2 shadow"
						>
							<li><a href="/delete">Delete</a></li>
						</ul>
					</div></th
				>
				<td></td>
				<td class="text-left"
					>{#if $eventsQueryResult.isLoading}
						<p class="loading loading-spinner loading-lg"></p>
					{/if}</td
				>
				<td class="text-right">
					<div class="join">
						<button
							class="btn btn-disabled btn-square btn-outline join-item btn-sm"
							aria-disabled="true">&lt;</button
						>
						<button class="btn btn-square btn-outline join-item btn-sm">&gt;</button>
					</div></td
				>
			</tr></tfoot
		>
	</table>
</div>
