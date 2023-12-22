<script lang="ts">
	import { type Readable } from 'svelte/store';
	import * as Table from '$comp/ui/table';
	import { flexRender, type Table as SvelteTable } from '@tanstack/svelte-table';

	import { DataTableColumnHeader, DataTableToolbar, DataTablePagination } from '.';

	type TData = $$Generic;
	export let table: Readable<SvelteTable<TData>>;
</script>

<div class="space-y-4">
	<DataTableToolbar {table} />
	<div class="border rounded-md">
		<Table.Root>
			<Table.Header>
				{#each $table.getHeaderGroups() as headerGroup}
					<Table.Row>
						{#each headerGroup.headers as header}
							<Table.Head>
								{#if header.column.columnDef.id !== 'select' && header.column.columnDef.id !== 'actions'}
									<DataTableColumnHeader
										><svelte:component
											this={flexRender(
												header.column.columnDef.header,
												header.getContext()
											)}
										/></DataTableColumnHeader
									>
								{:else}
									<svelte:component
										this={flexRender(
											header.column.columnDef.header,
											header.getContext()
										)}
									/>
								{/if}
							</Table.Head>
						{/each}
					</Table.Row>
				{/each}
			</Table.Header>
			<Table.Body>
				{#each $table.getRowModel().rows as row}
					<Table.Row>
						{#each row.getVisibleCells() as cell}
							<Table.Cell>
								{#if cell.id === 'task'}
									<div class="w-[80px]">
										<svelte:component
											this={flexRender(
												cell.column.columnDef.cell,
												cell.getContext()
											)}
										/>
									</div>
								{:else}
									<svelte:component
										this={flexRender(
											cell.column.columnDef.cell,
											cell.getContext()
										)}
									/>
								{/if}
							</Table.Cell>
						{/each}
					</Table.Row>
				{/each}
			</Table.Body>
		</Table.Root>
	</div>
	<DataTablePagination {table} />
</div>
