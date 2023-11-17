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
	class="flex fixed top-0 left-0 z-20 flex-col flex-shrink-0 pt-16 w-64 h-full duration-75 lg:flex transition-width {$isSidebarOpen
		? 'lg:w-64'
		: 'lg:w-16 hidden'}"
	aria-label="Sidebar"
>
	<div
		class="flex relative flex-col flex-1 pt-0 min-h-0 bg-white border-r border-gray-200 dark:bg-gray-800 dark:border-gray-700"
		on:mouseenter={() => onSidebarMouseEnter()}
		on:mouseleave={() => onSidebarMouseLeave()}
	>
		<div class="flex overflow-y-auto flex-col flex-1 pt-5 pb-4">
			<div
				class="flex-1 px-3 space-y-1 bg-white divide-y divide-gray-200 dark:bg-gray-800 dark:divide-gray-700"
			>
				<ul class="pb-2 space-y-2">
					<li>
						<form action="#" method="GET" class="lg:hidden">
							<!-- <SearchInput id="mobile-search" value={filter} /> -->
							<label for="mobile-search" class="sr-only">Search</label>
							<div class="relative">
								<div
									class="flex absolute inset-y-0 left-0 items-center pl-3 pointer-events-none"
								>
									<svg
										class="w-5 h-5 text-gray-500"
										fill="currentColor"
										viewBox="0 0 20 20"
										xmlns="http://www.w3.org/2000/svg"
										><path
											fill-rule="evenodd"
											d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z"
											clip-rule="evenodd"
										></path></svg
									>
								</div>
								<input
									type="text"
									name="email"
									id="mobile-search"
									class="bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block w-full pl-10 p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:placeholder-gray-400 dark:text-gray-200 dark:focus:ring-primary-500 dark:focus:border-primary-500"
									placeholder="Search"
								/>
							</div>
						</form>
					</li>
					<li>
						<a
							href="/next"
							class="flex items-center p-2 text-base font-normal text-gray-900 rounded-lg hover:bg-gray-100 group dark:text-gray-200 dark:hover:bg-gray-700"
						>
							<IconChartPie
								class="w-6 h-6 text-gray-500 transition duration-75 group-hover:text-gray-900 dark:text-gray-400 dark:group-hover:text-white"
							/>
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

<button
	class="fixed inset-0 z-10 bg-gray-900/50 dark:bg-gray-900/90 {!$isLargeScreen && $isSidebarOpen
		? ''
		: 'hidden'}"
	aria-label="Close sidebar"
	on:click={onBackdropClick}
></button>
