<script lang="ts">
	import type { ProblemDetails } from '$lib/api/FetchClient';

	export let name: string;
	export let value: unknown;
	export let problem: ProblemDetails | null = null;
	export let required: boolean = false;

	export let autocomplete: string | null = null;
	export let label: string | null = null;
	export let placeholder: string | null = 'Enter email address';

	$: error = problem?.errors?.[name];
	$: label = label ?? name.charAt(0).toUpperCase() + name.slice(1);

	function clearError() {
		problem = problem?.clear(name) || null;
	}
</script>

<div class="form-control">
	<label for={name} class="label">
		<span class="label-text">{label}</span>
		<slot name="label" />
	</label>
	<input
		id={name}
		type="email"
		{autocomplete}
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
