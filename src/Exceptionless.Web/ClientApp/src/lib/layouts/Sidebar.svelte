<script lang="ts">
	import IconChartPie from '~icons/mdi/chart-pie';
	import IconStream from '~icons/mdi/view-stream';

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
	class="flex fixed top-0 left-0 z-20 flex-col flex-shrink-0 pt-16 w-64 h-full duration-75 lg:flex transition-width bg-background text-foreground {$isSidebarOpen
		? 'lg:w-64'
		: 'lg:w-16 hidden'}"
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
						<SidebarMenuItem title="Dashboard" href="/next">
							<span slot="icon" let:iconClass>
								<IconChartPie class={iconClass}></IconChartPie>
							</span>
						</SidebarMenuItem>
					</li>
					<li>
						<SidebarMenuItem title="Stream" href="/next/stream">
							<span slot="icon" let:iconClass>
								<IconStream class={iconClass}></IconStream>
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
