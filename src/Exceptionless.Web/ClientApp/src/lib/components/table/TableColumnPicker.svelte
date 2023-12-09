<script lang="ts">
	import Checkbox from '$comp/primitives/Checkbox.svelte';

	import { Button } from '$comp/ui/button';
	import * as DropdownMenu from '$comp/ui/dropdown-menu';

	import type { Readable } from 'svelte/store';

	import ViewColumnIcon from '~icons/mdi/view-column';
	import type { Column, Table } from '@tanstack/svelte-table';

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;

	function getColumnId(column: Column<TData, unknown>): string {
		return column.columnDef.header as string;
	}
</script>

<DropdownMenu.Root>
	<DropdownMenu.Trigger asChild let:builder>
		<Button builders={[builder]} variant="outline" size="icon"><ViewColumnIcon /></Button>
	</DropdownMenu.Trigger>
	<DropdownMenu.Content class="w-44" align="end">
		<DropdownMenu.Label>
			<div class="flex items-center space-x-2">
				<Checkbox
					id="toggle-all"
					checked={$table.getIsAllColumnsVisible()}
					on:click={(e) => $table.getToggleAllColumnsVisibilityHandler()(e)}
					>Toggle All</Checkbox
				>
			</div>
		</DropdownMenu.Label>
		<DropdownMenu.Separator />
		{#each $table.getAllLeafColumns() as column}
			{#if column.getCanHide()}
				<DropdownMenu.Label>
					<div class="flex items-center space-x-2">
						<Checkbox
							id={getColumnId(column)}
							checked={column.getIsVisible()}
							on:click={() => column.toggleVisibility()}
							>{column.columnDef.header}</Checkbox
						>
					</div>
				</DropdownMenu.Label>
			{/if}
		{/each}
	</DropdownMenu.Content>
</DropdownMenu.Root>
