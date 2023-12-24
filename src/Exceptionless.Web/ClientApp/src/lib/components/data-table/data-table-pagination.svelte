<script lang="ts">
	import { derived, writable, type Readable } from 'svelte/store';
	import type { Table } from '@tanstack/svelte-table';

	import { Button } from '$comp/ui/button';
	import { ChevronRight, ChevronLeft, DoubleArrowLeft } from 'radix-icons-svelte';
	import * as Select from '$comp/ui/select';
	import Number from '$comp/formatters/Number.svelte';

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;

	const pageIndex = derived(table, ($table) => $table.getState().pagination.pageIndex);
	const rows = derived(table, ($table) => $table.getRowModel().rows);
	const selectedRows = derived(table, ($table) => $table.getSelectedRowModel().rows);
</script>

<div class="flex items-center justify-between px-2">
	<div class="flex-1 text-sm text-muted-foreground">
		{#if $selectedRows.length > 0}
			{Object.keys($selectedRows).length} of{' '}
			{$rows.length} row(s) selected.
		{/if}
	</div>
	<div class="flex items-center space-x-6 lg:space-x-8">
		<div class="flex items-center space-x-2">
			<p class="text-sm font-medium">Rows per page</p>
			<Select.Root
				onSelectedChange={(selected) => $table.setPageSize(selected?.value ?? 10)}
				selected={{ value: 10, label: '10' }}
			>
				<Select.Trigger class="w-[180px]">
					<Select.Value placeholder="Select page size" />
				</Select.Trigger>
				<Select.Content>
					<Select.Item value="10">10</Select.Item>
					<Select.Item value="20">20</Select.Item>
					<Select.Item value="30">30</Select.Item>
					<Select.Item value="40">40</Select.Item>
					<Select.Item value="50">50</Select.Item>
				</Select.Content>
			</Select.Root>
		</div>
		<div class="flex w-[100px] items-center justify-center text-sm font-medium">
			Page <Number value={$pageIndex + 1} /> of <Number value={$table.getPageCount()} />
		</div>
		<div class="flex items-center space-x-2">
			{#if $pageIndex > 1}
				<Button
					variant="outline"
					class="hidden w-8 h-8 p-0 lg:flex"
					on:click={() => $table.resetPageIndex(true)}
				>
					<span class="sr-only">Go to first page</span>
					<DoubleArrowLeft size={15} />
				</Button>
			{/if}
			<Button
				variant="outline"
				class="w-8 h-8 p-0"
				disabled={!$table.getCanPreviousPage()}
				on:click={() => $table.previousPage()}
			>
				<span class="sr-only">Go to previous page</span>
				<ChevronLeft size={15} />
			</Button>
			<Button
				variant="outline"
				class="w-8 h-8 p-0"
				disabled={!$table.getCanNextPage()}
				on:click={() => $table.nextPage()}
			>
				<span class="sr-only">Go to next page</span>
				<ChevronRight size={15} />
			</Button>
		</div>
	</div>
</div>
