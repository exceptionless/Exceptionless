<script lang="ts">
	import { Button, Dropdown, DropdownItem, Checkbox, DropdownDivider } from 'flowbite-svelte';
	import type { Readable } from 'svelte/store';

	import ViewColumnIcon from '~icons/mdi/view-column';
	import type { Table } from '@tanstack/svelte-table';

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;
</script>

<Button class="p-2 text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white"
	><ViewColumnIcon /></Button
>
<Dropdown class="w-44 p-3 space-y-3 text-sm" placement="bottom-end">
	<DropdownItem>
		<Checkbox
			class="text-gray-700 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-600 dark:hover:text-white"
			checked={$table.getIsAllColumnsVisible()}
			on:change={(e) => $table.getToggleAllColumnsVisibilityHandler()(e)}>Toggle All</Checkbox
		>
	</DropdownItem>
	<DropdownDivider divClass="text-gray-700 dark:text-gray-400" />
	{#each $table.getAllLeafColumns() as column}
		{#if column.getCanHide()}
			<DropdownItem>
				<Checkbox
					class="text-gray-700 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-600 dark:hover:text-white"
					checked={column.getIsVisible()}
					on:change={column.getToggleVisibilityHandler()}
					>{column.columnDef.header}</Checkbox
				>
			</DropdownItem>
		{/if}
	{/each}
</Dropdown>
