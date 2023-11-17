<script lang="ts">
	import { Button } from '$comp/ui/button';
	import { DropdownMenu } from '$comp/ui/dropdown-menu';
	import { Checkbox } from '$comp/ui/checkbox';
	import Label from '$comp/ui/label/label.svelte';

	import type { Readable } from 'svelte/store';

	import ViewColumnIcon from '~icons/mdi/view-column';
	import type { Table } from '@tanstack/svelte-table';

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;
</script>

<DropdownMenu.Root>
	<DropdownMenu.Trigger asChild let:builder>
		<Button builders={[builder]} variant="outline"><ViewColumnIcon /></Button>
	</DropdownMenu.Trigger>
	<DropdownMenu.Content class="w-44">
		<DropdownMenu.Label>
			<div class="flex items-center space-x-2">
				<Checkbox
					id="toggle-all"
					checked={$table.getIsAllColumnsVisible()}
					on:change={(e) => $table.getToggleAllColumnsVisibilityHandler()(e)}
				/>
				<div class="grid gap-1.5 leading-none">
					<Label
						for="toggle-all"
						class="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
					>
						Toggle All
					</Label>
				</div>
			</div>
		</DropdownMenu.Label>
		<DropdownMenu.Separator />
		{#each $table.getAllLeafColumns() as column}
			{#if column.getCanHide()}
				<DropdownMenu.Label>
					<div class="flex items-center space-x-2">
						<Checkbox
							id={column.columnDef.header}
							checked={column.getIsVisible()}
							on:change={column.getToggleVisibilityHandler()}
						/>
						<div class="grid gap-1.5 leading-none">
							<Label
								for={column.columnDef.header}
								class="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70"
							>
								{column.columnDef.header}
							</Label>
						</div>
					</div>
				</DropdownMenu.Label>
			{/if}
		{/each}
	</DropdownMenu.Content>
</DropdownMenu.Root>
