<script lang="ts">
	import type { Readable } from 'svelte/store';

	import { DEFAULT_LIMIT } from '$lib/helpers/api';

	import type { Table as TableType } from '@tanstack/svelte-table';
	import { createEventDispatcher } from 'svelte';
	import Table from './Table.svelte';
	import TableWithPagingFooter from './TableWithPagingFooter.svelte';

	type TData = $$Generic;
	export let table: Readable<TableType<TData>>;

	export let loading: boolean;
	export let error: string[] | undefined;

	export let page: number;
	export let pageTotal: number;
	export let limit = DEFAULT_LIMIT;
	export let total: number;

	export let onNavigateToFirstPage: () => void;
	export let onPreviousPage: () => void;
	export let onNextPage: () => void;

	const dispatch = createEventDispatcher();
</script>

<Table {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}>
	<slot slot="header" name="header" {table} />
	<slot
		slot="footer"
		name="footer"
		{table}
		{loading}
		{error}
		{page}
		{pageTotal}
		{limit}
		{total}
		{onNavigateToFirstPage}
		{onPreviousPage}
		{onNextPage}
	>
		<TableWithPagingFooter
			{loading}
			{error}
			{page}
			{pageTotal}
			{limit}
			{total}
			{onNavigateToFirstPage}
			{onPreviousPage}
			{onNextPage}
		></TableWithPagingFooter>
	</slot>
</Table>
