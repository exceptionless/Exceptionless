<script lang="ts">
	import IconSearch from '~icons/mdi/search';

	import logo from '$lib/assets/exceptionless-48.png';
	import { isPageWithSidebar, isSidebarOpen, isSidebarExpanded } from '$lib/stores/sidebar';
	import SearchInput from '$comp/SearchInput.svelte';
	import DarkModeButton from '$comp/DarkModeButton.svelte';

	let filter = '';

	function onHamburgerClick(): void {
		const shouldExpand = !$isSidebarExpanded;
		isSidebarExpanded.set(shouldExpand);
		isSidebarOpen.set(shouldExpand);
	}
</script>

<nav
	class="fixed z-30 w-full bg-white border-b border-gray-200 dark:bg-gray-800 dark:border-gray-700"
>
	<div class="py-3 px-3 lg:px-5 lg:pl-3">
		<div class="flex justify-between items-center">
			<div class="flex justify-start items-center">
				<button
					id="toggleSidebar"
					aria-expanded="true"
					aria-controls="sidebar"
					on:click={onHamburgerClick}
					class="hidden p-2 mr-3 text-gray-600 rounded cursor-pointer lg:inline hover:text-gray-900 hover:bg-gray-100 dark:text-gray-400 dark:hover:text-white dark:hover:bg-gray-700"
				>
					<svg
						class="w-6 h-6"
						fill="currentColor"
						viewBox="0 0 20 20"
						xmlns="http://www.w3.org/2000/svg"
					>
						<path
							fill-rule="evenodd"
							d="M3 5a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zM3 10a1 1 0 011-1h6a1 1 0 110 2H4a1 1 0 01-1-1zM3 15a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z"
							clip-rule="evenodd"
						></path>
					</svg>
				</button>
				<button
					id="toggleSidebarMobile"
					aria-expanded="true"
					aria-controls="sidebar"
					on:click={onHamburgerClick}
					class="p-2 mr-2 text-gray-600 rounded cursor-pointer lg:hidden hover:text-gray-900 hover:bg-gray-100 focus:bg-gray-100 dark:focus:bg-gray-700 focus:ring-2 focus:ring-gray-100 dark:focus:ring-gray-700 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-white"
				>
					<svg
						id="toggleSidebarMobileHamburger"
						class="w-6 h-6"
						fill="currentColor"
						viewBox="0 0 20 20"
						xmlns="http://www.w3.org/2000/svg"
					>
						<path
							fill-rule="evenodd"
							d="M3 5a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1zM3 10a1 1 0 011-1h6a1 1 0 110 2H4a1 1 0 01-1-1zM3 15a1 1 0 011-1h12a1 1 0 110 2H4a1 1 0 01-1-1z"
							clip-rule="evenodd"
						></path>
					</svg>
					<svg
						id="toggleSidebarMobileClose"
						class="hidden w-6 h-6"
						fill="currentColor"
						viewBox="0 0 20 20"
						xmlns="http://www.w3.org/2000/svg"
					>
						<path
							fill-rule="evenodd"
							d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
							clip-rule="evenodd"
						></path>
					</svg>
				</button>
				<a href="./" class="flex mr-14">
					<img src={logo} class="mr-3 h-8" alt="Exceptionless Logo" />
					<span
						class="self-center text-2xl font-semibold whitespace-nowrap dark:text-white"
						>Exceptionless</span
					>
				</a>
				<form action="/" method="GET" class="hidden lg:block lg:pl-2">
					<div class="mt-1 lg:w-96">
						<SearchInput id="topbar-search" value={filter} />
					</div>
				</form>
			</div>
			<div class="flex items-center">
				<button
					id="toggleSidebarMobileSearch"
					type="button"
					on:click={() => isSidebarOpen.set(!$isSidebarOpen)}
					class="p-2 text-gray-500 rounded-lg lg:hidden hover:text-gray-900 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-white"
				>
					<span class="sr-only">Search</span>

					<IconSearch class="w-6 h-6" />
				</button>

				<DarkModeButton></DarkModeButton>

				<div class="flex items-center ml-3">
					<div>
						<button
							type="button"
							class="flex text-sm bg-gray-800 rounded-full focus:ring-4 focus:ring-gray-300 dark:focus:ring-gray-600"
							id="user-menu-button-2"
							aria-expanded="false"
							data-dropdown-toggle="dropdown-2"
						>
							<span class="sr-only">Open user menu</span>
							<img
								class="w-8 h-8 rounded-full"
								src="//www.gravatar.com/avatar/89b10deee628535a5510db131f983541?default=mm&size=100"
								alt="user photo"
							/>
						</button>
					</div>

					<div
						class="hidden z-50 my-4 text-base list-none bg-white rounded divide-y divide-gray-100 shadow dark:bg-gray-700 dark:divide-gray-600"
						id="dropdown-2"
					>
						<div class="py-3 px-4" role="none">
							<p class="text-sm text-gray-900 dark:text-white" role="none">
								John Doe
							</p>
							<p
								class="text-sm font-medium text-gray-900 truncate dark:text-gray-300"
								role="none"
							>
								test@localhost
							</p>
						</div>
						<ul class="py-1" role="none">
							<li>
								<a
									href="/"
									class="block py-2 px-4 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-600 dark:hover:text-white"
									role="menuitem">Dashboard</a
								>
							</li>
							<li>
								<a
									href="/"
									class="block py-2 px-4 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-600 dark:hover:text-white"
									role="menuitem">Settings</a
								>
							</li>
							<li>
								<a
									href="/"
									class="block py-2 px-4 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-600 dark:hover:text-white"
									role="menuitem">Sign out</a
								>
							</li>
						</ul>
					</div>
				</div>
			</div>
		</div>
	</div>
</nav>
