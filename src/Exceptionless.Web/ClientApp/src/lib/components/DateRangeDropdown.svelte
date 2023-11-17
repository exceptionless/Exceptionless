<script lang="ts">
	import { Select } from 'flowbite-svelte';

	export let value: string;

	let items = [
		{ value: 'last hour', name: 'Last Hour' },
		{ value: 'last 24 hours', name: 'Last 24 Hours' },
		{ value: 'last week', name: 'Last Week' },
		{ value: 'last 30 days', name: 'Last 30 Days' },
		{ value: '', name: 'All Time' }
	];
</script>

<Select
	placeholder=""
	defaultClass="w-30 bg-white divide-gray-100 shadow dark:bg-gray-700 dark:divide-gray-600"
	{items}
	bind:value
/>
