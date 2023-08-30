<script lang="ts">
	import { onMount } from 'svelte';
	import { FetchClient } from '$lib/api/FetchClient';
	import type { PersistentEvent } from '$lib/models/api.generated';

	const api = new FetchClient();
	const loading = api.loading;
	let data: PersistentEvent[] = [];

	onMount(async () => {
		let response = await api.getJSON<PersistentEvent[]>('events');
		if (response.success && response.data) data = response.data;
	});
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
			<!-- row 1 -->
			<tr>
				<th>
					<label>
						<input type="checkbox" class="checkbox" />
					</label>
				</th>
				<td>Feature: Svelte</td>
				<td>Blake Niemyjski</td>
				<td>a few seconds ago</td>
			</tr>
			<!-- row 2 -->
			<tr class="hover">
				<th>
					<label>
						<input type="checkbox" class="checkbox" />
					</label>
				</th>
				<td>Feature: Is</td>
				<td>Blake Niemyjski</td>
				<td>a few seconds ago</td>
			</tr>
			<!-- row 3 -->
			<tr>
				<th>
					<label>
						<input type="checkbox" class="checkbox" />
					</label>
				</th>
				<td>Feature: Amazing</td>
				<td></td>
				<td>a few seconds ago</td>
			</tr>
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
					>{#if $loading}
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

{#if $loading}
	<p class="loading loading-spinner loading-lg"></p>
{:else}
	{#each data as item}
		<p>{item.message}</p>
	{/each}
{/if}
