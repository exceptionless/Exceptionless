<script lang="ts">
	import { onMount } from 'svelte';
	import { FetchClient, ProblemDetails } from '$lib/api/FetchClient';
	import type { PersistentEvent } from '$lib/models/api.generated';

	const api = new FetchClient();
	const loading = api.loading;
	let data: PersistentEvent[] = [];

	onMount(async () => {
		let response = await api.getJSON<PersistentEvent[]>('events');
		if (response.success && response.data) data = response.data;
	});
</script>

<h1>Welcome to SvelteKit</h1>
<p>Visit <a href="https://kit.svelte.dev">kit.svelte.dev</a> to read the documentation</p>

<h1 class="text-3xl font-bold underline">Hello world!</h1>

<button class="btn btn-primary">One</button>
<button class="btn btn-secondary">Two</button>
<button class="btn btn-accent btn-outline">Three</button>

<div class="overflow-x-auto">
	<table class="table">
		<!-- head -->
		<thead>
			<tr>
				<th>
					<label>
						<input type="checkbox" class="checkbox" />
					</label>
				</th>
				<th>Name</th>
				<th>Job</th>
				<th>Favorite Color</th>
				<th></th>
			</tr>
		</thead>
		<tbody>
			{#if $loading}
				<div>Loading...</div>
			{:else}
				{#each data as item}
					<tr>
						<th>
							<label>
								<input type="checkbox" class="checkbox" />
							</label>
						</th>
						<td>
							<div class="flex items-center space-x-3">
								<div class="avatar">
									<div class="mask mask-squircle h-12 w-12">
										<img
											src="/tailwind-css-component-profile-2@56w.png"
											alt="Avatar Tailwind CSS Component"
										/>
									</div>
								</div>
								<div>
									<div class="font-bold">Hart Hagerty</div>
									<div class="text-sm opacity-50">United States</div>
								</div>
							</div>
						</td>
						<td>
							Zemlak, Daniel and Leannon
							<br />
							<span class="badge badge-ghost badge-sm"
								>Desktop Support Technician</span
							>
						</td>
						<td>Purple</td>
						<th>
							<button class="btn btn-ghost btn-xs">details</button>
						</th>
					</tr>
				{/each}
			{/if}
		</tbody>
		<!-- foot -->
		<tfoot>
			<tr>
				<th></th>
				<th>Name</th>
				<th>Job</th>
				<th>Favorite Color</th>
				<th></th>
			</tr>
		</tfoot>
	</table>
</div>

<style lang="postcss">
	:global(html) {
		background-color: theme(colors.gray.100);
	}
</style>
