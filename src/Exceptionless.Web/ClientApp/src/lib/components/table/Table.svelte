<script lang="ts">
	import KeyboardArrowDownIcon from '~icons/mdi/keyboard-arrow-down';
	import KeyboardArrowUpIcon from '~icons/mdi/keyboard-arrow-up';

	import {
		Table,
		TableBody,
		TableBodyCell,
		TableBodyRow,
		TableHead,
		TableHeadCell
	} from 'flowbite-svelte';
	import { flexRender, type Header, type Table as TableType } from '@tanstack/svelte-table';
	import { createEventDispatcher } from 'svelte';
	import type { Readable } from 'svelte/store';

	type TData = $$Generic;
	export let table: Readable<TableType<TData>>;

	const dispatch = createEventDispatcher();

	function getHeaderColumnClass(header: Header<TData, unknown>) {
		const classes = [
			'p-4 text-xs font-medium tracking-wider text-left text-gray-500 uppercase dark:text-white',
			(header.column.columnDef.meta as { class?: string })?.class || ''
		];

		if (header.column.getCanSort()) {
			classes.push('hover cursor-pointer');
		}

		return classes.filter(Boolean).join(' ');
	}
</script>

<div class="flex flex-col">
	<div class="overflow-x-auto rounded-lg">
		<div class="inline-block min-w-full align-middle">
			<slot name="header" {table} />

			<Table
				striped={true}
				class="table-fixed min-w-full divide-y divide-gray-200 dark:divide-gray-600"
			>
				<TableHead theadClass="bg-gray-50 dark:bg-gray-700">
					{#each $table.getHeaderGroups() as headerGroup}
						{#each headerGroup.headers as header}
							{#if !header.isPlaceholder}
								<TableHeadCell
									class={getHeaderColumnClass(header)}
									on:click={header.column.getToggleSortingHandler()}
									disabled={!header.column.getCanSort()}
									scope="col"
								>
									<div class="flex items-center">
										<svelte:component
											this={flexRender(
												header.column.columnDef.header,
												header.getContext()
											)}
										/>
										{#if header.column.getIsSorted() === 'asc'}
											<KeyboardArrowUpIcon />
										{:else if header.column.getIsSorted() === 'desc'}
											<KeyboardArrowDownIcon />
										{/if}
									</div>
								</TableHeadCell>
							{:else}
								<TableHeadCell class={getHeaderColumnClass(header)}></TableHeadCell>
							{/if}
						{/each}
					{/each}
				</TableHead>
				<TableBody tableBodyClass="bg-white dark:bg-gray-800">
					<TableBodyRow class="hidden only:table-row text-center">
						<TableBodyCell
							colspan={$table.getVisibleLeafColumns().length}
							tdClass="p-4 text-sm font-normal text-gray-900 dark:text-white"
						>
							No data was found with the current filter.
						</TableBodyCell>
					</TableBodyRow>
					{#each $table.getRowModel().rows as row}
						<TableBodyRow
							class="hover cursor-pointer"
							on:click={() => dispatch('rowclick', row.original)}
						>
							{#each row.getVisibleCells() as cell}
								<TableBodyCell
									tdClass="p-4 text-sm font-normal text-gray-900 dark:text-white"
								>
									<svelte:component
										this={flexRender(
											cell.column.columnDef.cell,
											cell.getContext()
										)}
									/>
								</TableBodyCell>
							{/each}
						</TableBodyRow>
					{/each}
				</TableBody>
			</Table>

			<slot name="footer" {table} />
		</div>
	</div>
</div>
