<script lang="ts">
	import IconChartPie from '~icons/mdi/chart-pie';

	import { page } from '$app/stores';
	import { isSidebarOpen, isSidebarExpanded, isLargeScreen } from '$lib/stores/sidebar';
	import SearchInput from '$comp/SearchInput.svelte';

	let filter = '';

	function onSidebarMouseEnter(): void {
		if (!$isSidebarExpanded) {
			isSidebarOpen.set(true);
		}
	}

	function onSidebarMouseLeave(): void {
		if (!$isSidebarExpanded) {
			isSidebarOpen.set(false);
		}
	}

	function onBackdropClick() {
		isSidebarExpanded.set(false);
		isSidebarOpen.set(false);
	}
</script>

<aside
	id="sidebar"
	class="bg-secondary text-secondary-foreground flex fixed top-0 left-0 z-20 flex-col flex-shrink-0 pt-16 w-64 h-full duration-75 lg:flex transition-width {$isSidebarOpen
		? 'lg:w-64'
		: 'lg:w-16 hidden'}"
	aria-label="Sidebar"
>
	<div
		class="flex relative flex-col flex-1 pt-0 min-h-0 border-r"
		on:mouseenter={() => onSidebarMouseEnter()}
		on:mouseleave={() => onSidebarMouseLeave()}
		role="none"
	>
		<div class="flex overflow-y-auto flex-col flex-1 pt-5 pb-4">
			<div class="flex-1 px-3 space-y-1 divide-y">
				<ul class="pb-2 space-y-2">
					<li>
						<form action="#" method="GET" class="lg:hidden">
							<SearchInput id="mobile-search" value={filter} />
						</form>
					</li>
					<li>
						<a
							href="/next"
							class="flex items-center p-2 text-base font-normal rounded-lg group"
						>
							<IconChartPie class="w-6 h-6 transition duration-75 " />
							<span
								class="ml-3 {!$isSidebarOpen && $isLargeScreen
									? 'lg:hidden lg:absolute'
									: ''}">Dashboard</span
							>
						</a>
					</li>
				</ul>
			</div>
		</div>
	</div>
</aside>

<div
	class="fixed inset-0 z-10 bg-gray-900/50 dark:bg-gray-900/90 {!$isLargeScreen && $isSidebarOpen
		? ''
		: 'hidden'}"
	aria-label="Close sidebar"
	on:click={onBackdropClick}
	role="none"
></div>
