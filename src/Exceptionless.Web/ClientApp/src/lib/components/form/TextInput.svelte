<script lang="ts">
	import Input from '$comp/ui/input/input.svelte';
	import { Label } from '$comp/ui/label';
	import type { ProblemDetails } from '$lib/api/FetchClient';

	export let name: string;
	export let value: unknown;
	export let problem: ProblemDetails | null = null;
	export let required: boolean = false;

	export let autocomplete: string | null = null;
	export let label: string | undefined = undefined;
	export let minlength: number | undefined = undefined;
	export let maxlength: number | undefined = undefined;
	export let placeholder: string | undefined = undefined;

	$: error = problem?.errors?.[name];
	$: label = label ?? name.charAt(0).toUpperCase() + name.slice(1);

	function clearError() {
		problem = problem?.clear(name) || null;
	}
</script>

<div class="space-y-2">
	<Label for={name} class={error ? 'text-destructive' : ''}>
		{label}
		<slot name="label" />
	</Label>
	<Input
		id={name}
		type="text"
		{autocomplete}
		{placeholder}
		{minlength}
		{maxlength}
		class="w-full"
		on:change={clearError}
		bind:value
		{required}
	/>
	{#if error}
		<Label for={name} class="text-[0.8rem] font-medium text-destructive"
			>{error.join(' ')}</Label
		>
	{/if}
</div>
