<script lang="ts">
	import type { ProblemDetails } from '$lib/api/FetchClient';

	export let name: string;
	export let value: unknown;
	export let problem: ProblemDetails;
	export let required: boolean = false;

	export let label: string | undefined;
	export let placeholder: string | undefined;

	$: error = problem.errors?.[name];
	$: label = label ?? name.charAt(0).toUpperCase() + name.slice(1);

	function clearError() {
		problem = problem.clear(name);
	}
</script>

<div class="form-control">
	<label for={name} class="label">
		<span class="label-text">{label}</span>
		<slot name="label" />
	</label>
	<input
		id={name}
		type="text"
		{placeholder}
		class="input input-bordered input-primary w-full"
		class:input-error={error}
		on:change={clearError}
		bind:value
		{required}
	/>
	{#if error}
		<label for={name} class="label">
			<span class="label-text text-error">{error.join(' ')}</span>
		</label>
	{/if}
</div>
