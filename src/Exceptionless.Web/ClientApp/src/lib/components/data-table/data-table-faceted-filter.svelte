<script lang="ts">
	import {
		PlusCircled,
		Check,
		CheckCircled,
		Circle,
		CrossCircled,
		QuestionMarkCircled,
		Stopwatch
	} from 'radix-icons-svelte';
	import * as Command from '$comp/ui/command';
	import * as Popover from '$comp/ui/popover';
	import { Button } from '$comp/ui/button';
	import { cn } from '$lib/utils';
	import Separator from '$comp/ui/separator/separator.svelte';
	import Badge from '$comp/ui/badge/badge.svelte';

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

	export let filterValues: string[] = [];
	export let title: string;
	export let options = [] as typeof statuses;

	let open = false;

	const handleSelect = (currentValue: string) => {
		if (Array.isArray(filterValues) && filterValues.includes(currentValue)) {
			filterValues = filterValues.filter((v) => v !== currentValue);
		} else {
			filterValues = [...(Array.isArray(filterValues) ? filterValues : []), currentValue];
		}
	};
</script>

<Popover.Root bind:open>
	<Popover.Trigger asChild let:builder>
		<Button builders={[builder]} variant="outline" size="sm" class="h-8 border-dashed">
			<PlusCircled class="w-4 h-4 mr-2" />
			{title}

			{#if filterValues.length > 0}
				<Separator orientation="vertical" class="h-4 mx-2" />
				<Badge variant="secondary" class="px-1 font-normal rounded-sm lg:hidden">
					{filterValues.length}
				</Badge>
				<div class="hidden space-x-1 lg:flex">
					{#if filterValues.length > 2}
						<Badge variant="secondary" class="px-1 font-normal rounded-sm">
							{filterValues.length} Selected
						</Badge>
					{:else}
						{#each filterValues as option}
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
						<Command.Item value={option.value} onSelect={handleSelect}>
							<div
								class={cn(
									'mr-2 flex h-4 w-4 items-center justify-center rounded-sm border border-primary',
									filterValues.includes(option.value)
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
				{#if filterValues.length > 0}
					<Command.Separator />
					<Command.Item
						class="justify-center text-center"
						onSelect={() => {
							filterValues = [];
						}}
					>
						Clear filters
					</Command.Item>
				{/if}
			</Command.List>
		</Command.Root>
	</Popover.Content>
</Popover.Root>
