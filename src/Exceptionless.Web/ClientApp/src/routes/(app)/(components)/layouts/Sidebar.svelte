<script lang="ts">
	import IconEvents from '~icons/mdi/calendar-month-outline';
	import IconStacks from '~icons/mdi/checkbox-multiple-marked-outline';
	import IconEventLog from '~icons/mdi/sort-clock-descending-outline';

	import { isSidebarOpen, isLargeScreen } from '$lib/stores/sidebar';
	import SearchInput from '$comp/SearchInput.svelte';
	import SidebarMenuItem from './SidebarMenuItem.svelte';

	let filter = '';

	function onBackdropClick() {
		isSidebarOpen.set(false);
	}
</script>

<aside
	id="sidebar"
	class="transition-width fixed left-0 top-0 z-20 flex h-full w-64 flex-shrink-0 flex-col bg-background pt-16 text-foreground duration-75 lg:flex {$isSidebarOpen
		? 'lg:w-64'
		: 'hidden lg:w-16'}"
	aria-label="Sidebar"
>
	<div class="relative flex min-h-0 flex-1 flex-col border-r pt-0" role="none">
		<div class="flex flex-1 flex-col overflow-y-auto pb-4 pt-5">
			<div class="flex-1 space-y-1 divide-y px-3">
				<ul class="space-y-2 pb-2">
					<li>
						<form action="#" method="GET" class="lg:hidden">
							<SearchInput id="mobile-search" value={filter} />
						</form>
					</li>
					<li>
						<SidebarMenuItem title="Dashboard" href="/next/">
							<span slot="icon" let:iconClass>
								<IconEvents class={iconClass}></IconEvents>
							</span>
						</SidebarMenuItem>
					</li>
					<li>
						<SidebarMenuItem title="To-do List" href="/next/to-do-list">
							<span slot="icon" let:iconClass>
								<IconStacks class={iconClass}></IconStacks>
							</span>
						</SidebarMenuItem>
					</li>
					<li>
						<SidebarMenuItem title="Stream" href="/next/stream">
							<span slot="icon" let:iconClass>
								<IconEventLog class={iconClass}></IconEventLog>
							</span>
						</SidebarMenuItem>
					</li>
				</ul>
			</div>
		</div>
	</div>
</aside>

<button
	class="fixed inset-0 z-10 bg-gray-900/50 dark:bg-gray-900/90 {!$isLargeScreen && $isSidebarOpen
		? ''
		: 'hidden'}"
	title="Close sidebar"
	aria-label="Close sidebar"
	on:click={onBackdropClick}
></button>
