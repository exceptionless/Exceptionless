<script lang="ts">
	import { createSvelteTable } from '@tanstack/svelte-table';
	import InfiniteScroll from 'svelte-infinite-scroll';
	import {
		useGetEventsQuery,
		useGetEventsInfiniteQuery,
		type IGetEventsParams
	} from '$api/EventQueries';
	//	import { FetchClient, ProblemDetails } from '$lib/api/FetchClient';
	// 	import type { PersistentEvent } from '$lib/models/api';
	// 	import { createQuery } from '@tanstack/svelte-query';
	// 	import { parseNextPageQueryParameters, parsePreviousPageQueryParameters } from '$api/link';
	//
	// 	const api = new FetchClient();
	// 		const eventsQueryResult = createQuery<PersistentEvent[], ProblemDetails, PersistentEvent[]>(
	// 			['PersistentEvent'],
	// 			async () => {
	// 				const response = await api.getJSON<PersistentEvent[]>('events', {
	// 					params: { mode: 'summary' }
	// 				});
	// 				if (response.success) {
	// 					return response.data ?? [];
	// 				}
	//
	// 				throw response.problem;
	// 			}
	// 		);

	// Uncomment for paging
	// $: currentPageParams = { mode: 'summary' } as IGetEventsParams;
	// $: eventsQueryResult = useGetEventsQuery(currentPageParams);
	// $: linkHeader = $eventsQueryResult.data?.response.headers.get('link');
	// $: previousPageParams = parsePreviousPageQueryParameters(linkHeader) as IGetEventsParams;
	// $: nextPageParams = parseNextPageQueryParameters(linkHeader) as IGetEventsParams;

	const eventsQueryResult = useGetEventsInfiniteQuery({ mode: 'summary' });
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
				{#each $eventsQueryResult.data.pages as page}
					{#each page.data ?? [] as ev}
						<!-- Uncomment for paging
                    {#each $eventsQueryResult.data.data ?? [] as ev} -->
						<tr>
							<th>
								<label>
									<input type="checkbox" class="checkbox" />
								</label>
							</th>
							<td>{ev.data?.Message ?? ev.data?.Source}</td>
							<td>{ev.data?.Identity ?? ev.data?.Name}</td>
							<td>{ev.date}</td>
						</tr>
					{/each}
				{/each}
				<InfiniteScroll
					hasMore={$eventsQueryResult.hasNextPage &&
						!$eventsQueryResult.isFetchingNextPage}
					threshold={100}
					window={true}
					on:loadMore={() => {
						$eventsQueryResult.fetchNextPage();
					}}
				/>
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
					<!-- Uncomment for paging

                        <div class="join">
						<button
							class="btn btn-square btn-outline join-item btn-sm"
							disabled={!previousPageParams}
							on:click={() => {
								currentPageParams = previousPageParams;
								$eventsQueryResult.refetch();
							}}>&lt;</button
						>

						<button
							class="btn btn-square btn-outline join-item btn-sm"
							disabled={!nextPageParams}
							on:click={() => {
								currentPageParams = nextPageParams;
								$eventsQueryResult.refetch();
							}}>&gt;</button
						>
					</div> -->

					<!-- <button
                        on:click={() => $eventsQueryResult.fetchNextPage()}
                        disabled={!$eventsQueryResult.hasNextPage || $eventsQueryResult.isFetchingNextPage}>
                        {#if $eventsQueryResult.isFetching}
                            Loading more...
                        {:else if $eventsQueryResult.hasNextPage}
                            Load More
                        {/if}
                    </button> -->
				</td>
			</tr></tfoot
		>
	</table>
</div>
