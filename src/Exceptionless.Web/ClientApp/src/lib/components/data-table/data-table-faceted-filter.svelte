<script lang="ts">
	import { PlusCircled, Check } from 'radix-icons-svelte';
	import type { ComponentType } from 'svelte';
	import * as Command from '$comp/ui/command';
	import * as Popover from '$comp/ui/popover';
	import { Button } from '$comp/ui/button';
	import { cn } from '$lib/utils';
	import Separator from '$comp/ui/separator/separator.svelte';
	import Badge from '$comp/ui/badge/badge.svelte';

	type Option = {
		value: unknown;
		label: string;
		icon: ComponentType;
	};

	export let title: string;
	export let key: string;
	export let values: unknown[] = [];
	export let options: Option[] = [];
	export let onValueChange: (facetKey: string, updatedValues: unknown[]) => void;

	let open = false;

	export function onValueSelected(currentValue: unknown) {
		const updatedValues =
			Array.isArray(values) && values.includes(currentValue)
				? values.filter((v) => v !== currentValue)
				: [...(Array.isArray(values) ? values : []), currentValue];

		onValueChange(key, updatedValues);
	}

	export function onClearFilters() {
		onValueChange(key, []);
	}
</script>

<Popover.Root bind:open>
	<Popover.Trigger asChild let:builder>
		<Button builders={[builder]} variant="outline" size="sm" class="h-8 border-dashed">
			<PlusCircled class="w-4 h-4 mr-2" />
			{title}

			{#if values.length > 0}
				<Separator orientation="vertical" class="h-4 mx-2" />
				<Badge variant="secondary" class="px-1 font-normal rounded-sm lg:hidden">
					{values.length}
				</Badge>
				<div class="hidden space-x-1 lg:flex">
					{#if values.length > 2}
						<Badge variant="secondary" class="px-1 font-normal rounded-sm">
							{values.length} Selected
						</Badge>
					{:else}
						{#each values as option}
							<Badge variant="secondary" class="px-1 font-normal rounded-sm">
								{option}
							</Badge>
						{/each}
					{/if}
				</div>
			{/if}
		</Button>
	</Popover.Trigger>
	<Popover.Content class="w-[200px] p-0" align="start" side="bottom">
		<Command.Root>
			<Command.Input placeholder={title} />
			<Command.List>
				<Command.Empty>No results found.</Command.Empty>
				<Command.Group>
					{#each options as option}
						<Command.Item value={option.value} onSelect={onValueSelected}>
							<div
								class={cn(
									'mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary',
									values.includes(option.value)
										? 'bg-primary text-primary-foreground'
										: 'opacity-50 [&_svg]:invisible'
								)}
							>
								<Check className={cn('h-4 w-4')} />
							</div>
							<span>
								{option.label}
							</span>
						</Command.Item>
					{/each}
				</Command.Group>
				{#if values.length > 0}
					<Command.Separator />
					<Command.Item class="justify-center text-center" onSelect={onClearFilters}>
						Clear filters
					</Command.Item>
				{/if}
			</Command.List>
		</Command.Root>
	</Popover.Content>
</Popover.Root>
