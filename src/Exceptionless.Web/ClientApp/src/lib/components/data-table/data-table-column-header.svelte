<script lang="ts">
	import type { Column } from '@tanstack/svelte-table';
	import { ArrowDown, ArrowUp, CaretSort } from 'radix-icons-svelte';
	import { Button } from '$comp/ui/button';
	import * as DropdownMenu from '$comp/ui/dropdown-menu';
	import { cn } from '$lib/utils';

	type TData = $$Generic;
	export let column: Column<TData, unknown>;

	let className: string | undefined | null = undefined;
	export { className as class };

	function handleAscSort(e: Event) {
		if (column.getIsSorted() === 'asc') {
			return;
		}

		column.getToggleSortingHandler()?.(e);
	}

	function handleDescSort(e: Event) {
		if (column.getIsSorted() === 'desc') {
			return;
		}

		column.getToggleSortingHandler()?.(e);
	}
</script>

{#if column.getCanSort()}
	<div class={cn('flex items-center', className)}>
		<DropdownMenu.Root>
			<DropdownMenu.Trigger asChild let:builder>
				<Button
					variant="ghost"
					builders={[builder]}
					class="-ml-3 h-8 data-[state=open]:bg-accent"
				>
					<slot />
					{#if column.getIsSorted() === 'desc'}
						<ArrowDown class="w-4 h-4 ml-2" />
					{:else if column.getIsSorted() === 'asc'}
						<ArrowUp class="w-4 h-4 ml-2" />
					{:else}
						<CaretSort class="w-4 h-4 ml-2" />
					{/if}
				</Button>
			</DropdownMenu.Trigger>
			<DropdownMenu.Content align="start">
				<DropdownMenu.Item on:click={handleAscSort}>Asc</DropdownMenu.Item>
				<DropdownMenu.Item on:click={handleDescSort}>Desc</DropdownMenu.Item>
			</DropdownMenu.Content>
		</DropdownMenu.Root>
	</div>
{:else}
	<slot />
{/if}
