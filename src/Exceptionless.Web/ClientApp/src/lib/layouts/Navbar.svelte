<script lang="ts">
	import IconSearch from '~icons/mdi/search';
	import IconMoonWaningCrescent from '~icons/mdi/moon-waning-crescent';
	import IconWhiteBalanceSunny from '~icons/mdi/white-balance-sunny';

	import logo from '$lib/assets/exceptionless-48.png';
	import { isPageWithSidebar, isSidebarOpen, isSidebarExpanded } from '$lib/stores/sidebar';
	import SearchInput from '$comp/SearchInput.svelte';

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
				<form action="#" method="GET" class="hidden lg:block lg:pl-2">
					<!-- <SearchInput id="topbar-search" value={filter} /> -->
					<label for="topbar-search" class="sr-only">Search</label>
					<div class="relative mt-1 lg:w-96">
						<div
							class="flex absolute inset-y-0 left-0 items-center pl-3 pointer-events-none"
						>
							<svg
								class="w-5 h-5 text-gray-500 dark:text-gray-400"
								fill="currentColor"
								viewBox="0 0 20 20"
								xmlns="http://www.w3.org/2000/svg"
							>
								<path
									fill-rule="evenodd"
									d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z"
									clip-rule="evenodd"
								></path>
							</svg>
						</div>
						<input
							type="text"
							name="email"
							id="topbar-search"
							class="bg-gray-50 border border-gray-300 text-gray-900 sm:text-sm rounded-lg focus:ring-primary-500 focus:border-primary-500 block w-full pl-10 p-2.5 dark:bg-gray-700 dark:border-gray-600 dark:placeholder-gray-400 dark:text-white dark:focus:ring-primary-500 dark:focus:border-primary-500"
							placeholder="Search"
						/>
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

				<button
					id="theme-toggle"
					data-tooltip-target="tooltip-toggle"
					type="button"
					class="text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 focus:outline-none focus:ring-4 focus:ring-gray-200 dark:focus:ring-gray-700 rounded-lg text-sm p-2.5"
				>
					<svg
						id="theme-toggle-dark-icon"
						class="hidden w-5 h-5"
						fill="currentColor"
						viewBox="0 0 20 20"
						xmlns="http://www.w3.org/2000/svg"
					>
						<path d="M17.293 13.293A8 8 0 016.707 2.707a8.001 8.001 0 1010.586 10.586z"
						></path>
					</svg>
					<!-- <IconMoonWaningCrescent /> -->
					<svg
						id="theme-toggle-light-icon"
						class="hidden w-5 h-5"
						fill="currentColor"
						viewBox="0 0 20 20"
						xmlns="http://www.w3.org/2000/svg"
					>
						<path
							d="M10 2a1 1 0 011 1v1a1 1 0 11-2 0V3a1 1 0 011-1zm4 8a4 4 0 11-8 0 4 4 0 018 0zm-.464 4.95l.707.707a1 1 0 001.414-1.414l-.707-.707a1 1 0 00-1.414 1.414zm2.12-10.607a1 1 0 010 1.414l-.706.707a1 1 0 11-1.414-1.414l.707-.707a1 1 0 011.414 0zM17 11a1 1 0 100-2h-1a1 1 0 100 2h1zm-7 4a1 1 0 011 1v1a1 1 0 11-2 0v-1a1 1 0 011-1zM5.05 6.464A1 1 0 106.465 5.05l-.708-.707a1 1 0 00-1.414 1.414l.707.707zm1.414 8.486l-.707.707a1 1 0 01-1.414-1.414l.707-.707a1 1 0 011.414 1.414zM4 11a1 1 0 100-2H3a1 1 0 000 2h1z"
							fill-rule="evenodd"
							clip-rule="evenodd"
						></path>
					</svg>
					<!-- <IconWhiteBalanceSunny /> -->
				</button>
				<div
					id="tooltip-toggle"
					role="tooltip"
					class="inline-block absolute invisible z-10 py-2 px-3 text-sm font-medium text-white bg-gray-900 rounded-lg shadow-sm opacity-0 transition-opacity duration-300 tooltip"
				>
					Toggle dark mode
					<div class="tooltip-arrow" data-popper-arrow></div>
				</div>

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
									href="#"
									class="block py-2 px-4 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-600 dark:hover:text-white"
									role="menuitem">Dashboard</a
								>
							</li>
							<li>
								<a
									href="#"
									class="block py-2 px-4 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-600 dark:hover:text-white"
									role="menuitem">Settings</a
								>
							</li>
							<li>
								<a
									href="#"
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

<Navbar
	let:NavContainer
	fluid={true}
	class="fixed z-30 w-full bg-white border-b border-gray-200 dark:bg-gray-800 dark:border-gray-700             px-0 sm:px-0 py-0"
>
	<NavContainer
		fluid={true}
		class="py-3 px-3 lg:px-5 lg:pl-3                                                                                  mx-0"
	>
		<div class="flex justify-start items-center">
			{#if isPageWithSidebar}
				<NavHamburger
					onClick={onHamburgerClick}
					class="p-2 mr-2 lg:mr-3 text-gray-600 rounded cursor-pointer lg:inline hover:text-gray-900 hover:bg-gray-100 focus:bg-gray-100 dark:focus:bg-gray-700 focus:ring-2 focus:ring-gray-100 dark:focus:ring-gray-700 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-white     ml-0 md:block"
				></NavHamburger>
			{/if}
			<NavBrand href="/next" class="flex mr-14">
				<img src={logo} class="mr-3 h-8" alt="Exceptionless Logo" />
				<span class="self-center text-2xl font-semibold whitespace-nowrap dark:text-white"
					>Exceptionless</span
				>
			</NavBrand>

			<form action="#" method="GET" class="hidden lg:block lg:pl-2">
				<div class="mt-1 lg:w-96">
					<SearchInput id="topbar-search" value={filter} />
				</div>
			</form>
		</div>
		<div class="flex items-center">
			<Button
				on:click={() => isSidebarOpen.set(!$isSidebarOpen)}
				aria-label="Search"
				class="p-2 text-gray-500 rounded-lg lg:hidden hover:text-gray-900 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-white"
			>
				<IconSearch class="w-6 h-6" />
			</Button>

			<DarkMode
				btnClass="p-2 text-gray-500 rounded-lg hover:text-gray-900 hover:bg-gray-100 dark:text-gray-400 dark:hover:text-white dark:hover:bg-gray-700"
			>
				<svelte:fragment slot="lightIcon">
					<IconWhiteBalanceSunny />
				</svelte:fragment>
				<svelte:fragment slot="darkIcon">
					<IconMoonWaningCrescent />
				</svelte:fragment>
			</DarkMode>

			<div class="flex items-center ml-3">
				<div>
					<Button
						id="avatar-menu"
						class="flex text-sm bg-gray-800 rounded-full focus:ring-4 focus:ring-gray-300 dark:focus:ring-gray-600"
					>
						<span class="sr-only">Open user menu</span>
						<Avatar
							src="//www.gravatar.com/avatar/89b10deee628535a5510db131f983541?default=mm&size=100"
							class="w-8 h-8"
							alt="User Avatar"
						/>
					</Button>
				</div>

				<Dropdown
					placement="bottom"
					triggeredBy="#avatar-menu"
					containerClass="my-4 text-base list-none bg-white rounded divide-y divide-gray-100 shadow dark:bg-gray-700 dark:divide-gray-600"
				>
					<DropdownHeader class="py-3 px-4">
						<p class="text-sm text-gray-900 dark:text-white" role="none">John Doe</p>
						<p
							class="text-sm font-medium text-gray-900 truncate dark:text-gray-300"
							role="none"
						>
							test@localhost
						</p>
					</DropdownHeader>
					<DropdownItem
						defaultClass="block py-2 px-4 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-600 dark:hover:text-white text-left"
						>Dashboard</DropdownItem
					>
					<DropdownItem
						defaultClass="block py-2 px-4 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-600 dark:hover:text-white text-left"
						>Settings</DropdownItem
					>
					<DropdownDivider />
					<DropdownItem
						defaultClass="block py-2 px-4 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-600 dark:hover:text-white text-left"
						>Sign out</DropdownItem
					>
				</Dropdown>
			</div>
		</div>
	</NavContainer>
</Navbar>
