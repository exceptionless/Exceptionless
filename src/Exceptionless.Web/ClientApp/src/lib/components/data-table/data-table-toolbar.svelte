<script lang="ts">
	import type { Table } from '@tanstack/svelte-table';

	import { Input } from '$comp/ui/input';
	import { DataTableFacetedFilter, DataTableViewOptions } from '.';
	import Button from '$comp/ui/button/button.svelte';
	import {
		ArrowDown,
		ArrowRight,
		ArrowUp,
		CheckCircled,
		Circle,
		Cross2,
		CrossCircled,
		QuestionMarkCircled,
		Stopwatch
	} from 'radix-icons-svelte';

	import { writable, type Readable, type Writable } from 'svelte/store';

	const statuses = [
		{
			value: 'backlog',
			label: 'Backlog',
			icon: QuestionMarkCircled
		},
		{
			value: 'todo',
			label: 'Todo',
			icon: Circle
		},
		{
			value: 'in progress',
			label: 'In Progress',
			icon: Stopwatch
		},
		{
			value: 'done',
			label: 'Done',
			icon: CheckCircled
		},
		{
			value: 'canceled',
			label: 'Canceled',
			icon: CrossCircled
		}
	];

	const priorities = [
		{
			label: 'Low',
			value: 'low',
			icon: ArrowDown
		},
		{
			label: 'Medium',
			value: 'medium',
			icon: ArrowRight
		},
		{
			label: 'High',
			value: 'high',
			icon: ArrowUp
		}
	];

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;

	const filterValue: Writable<string> = writable('');
	const filterValues: Writable<{
		status: string[];
		priority: string[];
	}> = writable({
		status: [],
		priority: []
	});

	$: showReset = Object.values($filterValues).some((v) => v.length > 0);
</script>

<div class="flex items-center justify-between">
	<div class="flex items-center flex-1 space-x-2">
		<Input
			placeholder="Filter tasks..."
			class="h-8 w-[150px] lg:w-[250px]"
			type="text"
			bind:value={$filterValue}
		/>

		<DataTableFacetedFilter
			bind:filterValues={$filterValues.status}
			title="Status"
			options={statuses}
		/>
		<DataTableFacetedFilter
			bind:filterValues={$filterValues.priority}
			title="Priority"
			options={priorities}
		/>
		{#if showReset}
			<Button
				on:click={() => {
					$filterValues.status = [];
					$filterValues.priority = [];
				}}
				variant="ghost"
				class="h-8 px-2 lg:px-3"
			>
				Reset
				<Cross2 class="w-4 h-4 ml-2" />
			</Button>
		{/if}
	</div>

	<DataTableViewOptions {table} />
</div>
