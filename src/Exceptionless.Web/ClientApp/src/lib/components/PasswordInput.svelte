<script lang="ts">
	import type { ProblemDetails } from '$lib/api/FetchClient';

	export let name: string;
	export let value: any;
	export let problem: ProblemDetails;
	export let required: boolean = false;

	export let label: string | undefined;
	export let placeholder: string | undefined;

	let error = problem.errors?.[name];
</script>

<div class="form-control">
	<label for={name} class={label ?? name}>
		<span class="label-text">{label ?? name}</span>
		<slot name="label" />
	</label>
	<input
		id={name}
		type="password"
		{placeholder}
		class="input input-bordered input-primary w-full"
		class:input-error={error}
		on:change={() => problem.clear(name)}
		bind:value
		{required}
	/>
	{#if error}
		<label for={name} class="label">
			<span class="label-text text-error">{error.join(' ')}</span>
		</label>
	{/if}
</div>
