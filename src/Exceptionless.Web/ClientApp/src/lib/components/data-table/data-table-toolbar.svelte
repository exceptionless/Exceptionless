<script lang="ts">
	import type { Table } from '@tanstack/svelte-table';
	import { type Readable } from 'svelte/store';

	import DataTableViewOptions from './data-table-view-options.svelte';

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;
</script>

<div class="flex items-center justify-between">
	<div class="flex flex-1 items-center space-x-2">
		<slot />
	</div>

	<DataTableViewOptions {table} />
</div>
