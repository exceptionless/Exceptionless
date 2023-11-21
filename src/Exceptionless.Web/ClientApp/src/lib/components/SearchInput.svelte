<script lang="ts">
	import IconSearch from '~icons/mdi/search';
	import { Input } from '$comp/ui/input';

	export let id: string = 'search';
	export let placeholder: string = 'Search...';
	export let value: string;

	export let onChanged: (event: Event) => void = () => {};
</script>

<label for={id} class="sr-only">Search</label>
<div class="relative">
	<div class="flex absolute inset-y-0 left-0 items-center pl-3 pointer-events-none">
		<IconSearch class="w-5 h-5" />
	</div>
	<Input
		type="text"
		name="search"
		{id}
		bind:value
		on:change={onChanged}
		class="pl-10"
		{placeholder}
	/>
</div>
