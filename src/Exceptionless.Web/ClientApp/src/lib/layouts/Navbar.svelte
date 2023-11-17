<script lang="ts">
	import {
		Avatar,
		Button,
		Dropdown,
		DropdownItem,
		DropdownHeader,
		DropdownDivider,
		DarkMode,
		Navbar,
		NavBrand,
		NavHamburger
	} from 'flowbite-svelte';
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
