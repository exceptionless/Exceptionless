<script lang="ts">
	import type { Readable } from "svelte/store";

	import ViewColumnIcon from '~icons/mdi/view-column';
	import type { Table } from '@tanstack/svelte-table';

	type TData = $$Generic;
	export let table: Readable<Table<TData>>;
</script>

<div class="dropdown dropdown-bottom dropdown-end">
	<button tabindex="0" class="btn btn-xs btn-square"><ViewColumnIcon /></button>
	<!-- svelte-ignore a11y-no-noninteractive-tabindex -->
	<div tabindex="0" class="dropdown-content z-[1] menu shadow bg-base-100 rounded-box w-52 p-2">
		<div class="form-control">
			<label class="cursor-pointer label">
				<span class="label-text">Toggle All</span>
				<input
					type="checkbox"
					checked={$table.getIsAllColumnsVisible()}
					on:change={(e) => $table.getToggleAllColumnsVisibilityHandler()(e)}
					class="checkbox checkbox-xs"
				/>
			</label>
		</div>

		<div class="divider m-0"></div>
		{#each $table.getAllLeafColumns() as column}
			{#if column.getCanHide()}
				<div class="form-control">
					<label class="cursor-pointer label">
						<span class="label-text">{column.columnDef.header}</span>
						<input
							type="checkbox"
							checked={column.getIsVisible()}
							on:change={column.getToggleVisibilityHandler()}
							class="checkbox checkbox-xs"
						/>
					</label>
				</div>
			{/if}
		{/each}
	</div>
</div>
