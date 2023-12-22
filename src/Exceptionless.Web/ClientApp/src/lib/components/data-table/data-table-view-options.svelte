<script lang="ts">
	import type { Readable } from 'svelte/store';
	import { MixerHorizontal } from 'radix-icons-svelte'; // TODO replace with ViewColumnIcon
	import { Button } from '$comp/ui/button';
	import * as DropdownMenu from '$comp/ui/dropdown-menu';

	import type { Table } from '@tanstack/svelte-table';

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;
</script>

<DropdownMenu.Root>
	<DropdownMenu.Trigger asChild let:builder>
		<Button variant="outline" size="sm" class="hidden h-8 ml-auto lg:flex" builders={[builder]}>
			<MixerHorizontal class="w-4 h-4 mr-2" />
			View
		</Button>
	</DropdownMenu.Trigger>
	<DropdownMenu.Content>
		<DropdownMenu.Label>Toggle columns</DropdownMenu.Label>
		<DropdownMenu.Separator />
		{#each $table.getAllLeafColumns() as column}
			{#if column.getCanHide()}
				<DropdownMenu.CheckboxItem
					checked={column.getIsVisible()}
					on:click={() => column.toggleVisibility()}
				>
					{column.columnDef.header}
				</DropdownMenu.CheckboxItem>
			{/if}
		{/each}
	</DropdownMenu.Content>
</DropdownMenu.Root>
