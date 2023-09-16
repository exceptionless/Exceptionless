<script lang="ts">
	import KeyboardArrowDown from '~icons/mdi/keyboard-arrow-down';
	import KeyboardArrowUp from '~icons/mdi/keyboard-arrow-up';

	import { flexRender, type Table } from '@tanstack/svelte-table';
	import { createEventDispatcher } from 'svelte';
	import type { Readable } from 'svelte/store';

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;

	const dispatch = createEventDispatcher();
</script>

<table class="table table-zebra table-xs border">
	<thead>
		{#each $table.getHeaderGroups() as headerGroup}
			<tr>
				{#each headerGroup.headers as header}
					<th>
						{#if !header.isPlaceholder}
							<div
                                class="flex items-center"
								class:cursor-pointer={header.column.getCanSort()}
								class:select-none={header.column.getCanSort()}
								on:click={header.column.getToggleSortingHandler()}
                                on:keydown={header.column.getToggleSortingHandler()}
                                tabindex="0"
                                role="button"
							>
								<svelte:component
									this={flexRender(
										header.column.columnDef.header,
										header.getContext()
									)}
								/>
                                {#if header.column.getIsSorted() === 'asc'}
                                    <KeyboardArrowUp />
                                {:else if header.column.getIsSorted() === 'desc'}
                                    <KeyboardArrowDown />
                                {/if}
							</div>
						{/if}
					</th>
				{/each}
			</tr>
		{/each}
	</thead>
	<tbody>
		{#each $table.getRowModel().rows as row}
			<tr
				class="hover cursor-pointer"
				on:click|preventDefault={() => dispatch('rowclick', row.original)}
			>
				{#each row.getVisibleCells() as cell}
					<td>
						<svelte:component
							this={flexRender(cell.column.columnDef.cell, cell.getContext())}
						/>
					</td>
				{/each}
			</tr>
		{/each}
	</tbody>
</table>
