<script lang="ts">
	import IconSearch from '~icons/mdi/search';

	export let id: string = 'search';
	export let placeholder: string = 'Search...';
	export let value: string;

	export let onChanged: (event: Event) => void = () => {};
</script>

<label for={id} class="sr-only">Search</label>
<div class="relative">
	<div class="flex absolute inset-y-0 left-0 items-center pl-3 pointer-events-none">
		<IconSearch class="w-5 h-5 text-gray-500 dark:text-gray-400" />
	</div>
	<input
		type="text"
		name="search"
		{id}
		bind:value
		on:change={onChanged}
		class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block w-full pl-10 p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:placeholder-gray-400 dark:text-gray-200 dark:focus:ring-primary-500 dark:focus:border-primary-500"
		{placeholder}
	/>
</div>
