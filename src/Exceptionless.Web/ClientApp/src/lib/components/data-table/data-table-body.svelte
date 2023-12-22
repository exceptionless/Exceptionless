<script lang="ts">
	import { createEventDispatcher } from 'svelte';
	import { type Readable } from 'svelte/store';
	import { flexRender, type Header, type Table as SvelteTable } from '@tanstack/svelte-table';

	import DataTableColumnHeader from './data-table-column-header.svelte';
	import * as Table from '$comp/ui/table';

	type TData = $$Generic;
	export let table: Readable<SvelteTable<TData>>;

	const dispatch = createEventDispatcher();

	function getHeaderColumnClass(header: Header<TData, unknown>) {
		const classes = [(header.column.columnDef.meta as { class?: string })?.class || ''];

		return classes.filter(Boolean).join(' ');
	}
</script>

<div class="border rounded-md">
	<Table.Root>
		<Table.Header>
			{#each $table.getHeaderGroups() as headerGroup}
				<Table.Row>
					{#each headerGroup.headers as header}
						<Table.Head class={getHeaderColumnClass(header)}>
							<DataTableColumnHeader column={header.column}
								><svelte:component
									this={flexRender(
										header.column.columnDef.header,
										header.getContext()
									)}
								/></DataTableColumnHeader
							>
						</Table.Head>
					{/each}
				</Table.Row>
			{/each}
		</Table.Header>
		<Table.Body>
			<Table.Row class="hidden text-center only:table-row">
				<Table.Cell colspan={$table.getVisibleLeafColumns().length}>
					No data was found with the current filter.
				</Table.Cell>
			</Table.Row>
			{#each $table.getRowModel().rows as row}
				<Table.Row
					class="cursor-pointer hover"
					on:click={() => dispatch('rowclick', row.original)}
				>
					{#each row.getVisibleCells() as cell}
						<Table.Cell>
							<svelte:component
								this={flexRender(cell.column.columnDef.cell, cell.getContext())}
							/>
						</Table.Cell>
					{/each}
				</Table.Row>
			{/each}
		</Table.Body>
	</Table.Root>
</div>
