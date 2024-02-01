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
	<div class="relative flex flex-col flex-1 min-h-0 pt-0 border-r" role="none">
		<div class="flex flex-col flex-1 pt-5 pb-4 overflow-y-auto">
			<div class="flex-1 px-3 space-y-1 divide-y">
				<ul class="pb-2 space-y-2">
					<li>
						<form action="#" method="GET" class="lg:hidden">
							<SearchInput id="mobile-search" value={filter} />
						</form>
					</li>
					<li>
						<SidebarMenuItem title="Events" href="/next/">
							<span slot="icon" let:iconClass>
								<IconEvents class={iconClass}></IconEvents>
							</span>
						</SidebarMenuItem>
					</li>
					<li>
						<SidebarMenuItem title="Issues" href="/next/issues">
							<span slot="icon" let:iconClass>
								<IconStacks class={iconClass}></IconStacks>
							</span>
						</SidebarMenuItem>
					</li>
					<li>
						<SidebarMenuItem title="Event Stream" href="/next/stream">
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
