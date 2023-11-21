<script lang="ts">
	import KeyboardArrowDownIcon from '~icons/mdi/keyboard-arrow-down';
	import KeyboardArrowUpIcon from '~icons/mdi/keyboard-arrow-up';

	import * as Table from '$comp/ui/table';
	import { flexRender, type Header, type Table as TableType } from '@tanstack/svelte-table';
	import { createEventDispatcher } from 'svelte';
	import type { Readable } from 'svelte/store';

	type TData = $$Generic;
	export let table: Readable<TableType<TData>>;

	const dispatch = createEventDispatcher();

	function getHeaderColumnClass(header: Header<TData, unknown>) {
		const classes = [
			'tracking-wider uppercase',
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

			<Table.Root>
				<Table.Header>
					{#each $table.getHeaderGroups() as headerGroup}
						<Table.Row>
							{#each headerGroup.headers as header}
								{#if !header.isPlaceholder}
									<Table.Head class={getHeaderColumnClass(header)}>
										<button
											class="flex items-center w-full {getHeaderColumnClass(
												header
											)}"
											on:click={header.column.getToggleSortingHandler()}
											disabled={!header.column.getCanSort()}
										>
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
										</button>
									</Table.Head>
								{:else}
									<Table.Head class={getHeaderColumnClass(header)}></Table.Head>
								{/if}
							{/each}
						</Table.Row>
					{/each}
				</Table.Header>
				<Table.Body>
					<Table.Row class="hidden only:table-row text-center">
						<Table.Cell colspan={$table.getVisibleLeafColumns().length}>
							No data was found with the current filter.
						</Table.Cell>
					</Table.Row>
					{#each $table.getRowModel().rows as row}
						<Table.Row
							class="hover cursor-pointer"
							on:click={() => dispatch('rowclick', row.original)}
						>
							{#each row.getVisibleCells() as cell}
								<Table.Cell>
									<svelte:component
										this={flexRender(
											cell.column.columnDef.cell,
											cell.getContext()
										)}
									/>
								</Table.Cell>
							{/each}
						</Table.Row>
					{/each}
				</Table.Body>
			</Table.Root>

			<slot name="footer" {table} />
		</div>
	</div>
</div>
