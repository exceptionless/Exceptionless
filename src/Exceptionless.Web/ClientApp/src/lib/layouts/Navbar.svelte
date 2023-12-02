<script lang="ts">
	import IconSearch from '~icons/mdi/search';

	import logo from '$lib/assets/exceptionless-48.png';
	import { isSidebarOpen, isSidebarExpanded } from '$lib/stores/sidebar';
	import * as Avatar from '$comp/ui/avatar';
	import * as DropdownMenu from '$comp/ui/dropdown-menu';
	import SearchInput from '$comp/SearchInput.svelte';
	import DarkModeButton from '$comp/DarkModeButton.svelte';
	import { Button } from '$comp/ui/button';

	let filter = '';

	function onHamburgerClick(): void {
		const shouldExpand = !$isSidebarExpanded;
		isSidebarExpanded.set(shouldExpand);
		isSidebarOpen.set(shouldExpand);
	}
</script>

<nav class="fixed z-30 w-full border-b bg-background text-foreground">
	<div class="px-3 py-3 lg:px-5 lg:pl-3">
		<div class="flex items-center justify-between">
			<div class="flex items-center justify-start">
				<button
					id="toggleSidebar"
					aria-expanded="true"
					aria-controls="sidebar"
					on:click={onHamburgerClick}
					class="hidden p-2 mr-3 rounded cursor-pointer lg:inline"
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
					class="p-2 mr-2 rounded cursor-pointer lg:hidden focus:ring-2"
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
					<img src={logo} class="h-8 mr-3" alt="Exceptionless Logo" />
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
					class="p-2 rounded-lg lg:hidden"
				>
					<span class="sr-only">Search</span>

					<IconSearch class="w-6 h-6" />
				</button>

				<DarkModeButton></DarkModeButton>

				<div class="ml-3">
					<DropdownMenu.Root positioning={{ placement: 'bottom-end' }}>
						<DropdownMenu.Trigger asChild let:builder>
							<Button
								builders={[builder]}
								size="icon"
								variant="ghost"
								class="rounded-full"
							>
								<Avatar.Root title="TODO" class="h-7 w-7">
									<Avatar.Image
										src="//www.gravatar.com/avatar/89b10deee628535a5510db131f983541?default=mm&size=100"
										alt="gravatar"
									/>
									<Avatar.Fallback>TODO</Avatar.Fallback>
								</Avatar.Root>
							</Button>
						</DropdownMenu.Trigger>
						<DropdownMenu.Content class="w-56">
							<DropdownMenu.Label>My Account</DropdownMenu.Label>
							<DropdownMenu.Separator />
							<DropdownMenu.Group>
								<DropdownMenu.Item>
									Profile
									<DropdownMenu.Shortcut>⇧⌘P</DropdownMenu.Shortcut>
								</DropdownMenu.Item>
								<DropdownMenu.Item>
									Billing
									<DropdownMenu.Shortcut>⌘B</DropdownMenu.Shortcut>
								</DropdownMenu.Item>
								<DropdownMenu.Item>
									Settings
									<DropdownMenu.Shortcut>⌘S</DropdownMenu.Shortcut>
								</DropdownMenu.Item>
								<DropdownMenu.Item>
									Keyboard shortcuts
									<DropdownMenu.Shortcut>⌘K</DropdownMenu.Shortcut>
								</DropdownMenu.Item>
							</DropdownMenu.Group>
							<DropdownMenu.Separator />
							<DropdownMenu.Group>
								<DropdownMenu.Item>Team</DropdownMenu.Item>
								<DropdownMenu.Item>
									Invite users
									<DropdownMenu.Shortcut>⌘+I</DropdownMenu.Shortcut>
								</DropdownMenu.Item>
								<DropdownMenu.Item>
									New Team
									<DropdownMenu.Shortcut>⌘+T</DropdownMenu.Shortcut>
								</DropdownMenu.Item>
							</DropdownMenu.Group>
							<DropdownMenu.Separator />
							<DropdownMenu.Item>GitHub</DropdownMenu.Item>
							<DropdownMenu.Item>Support</DropdownMenu.Item>
							<DropdownMenu.Item>API</DropdownMenu.Item>
							<DropdownMenu.Separator />
							<DropdownMenu.Item>
								Sign out
								<DropdownMenu.Shortcut>⇧⌘Q</DropdownMenu.Shortcut>
							</DropdownMenu.Item>
						</DropdownMenu.Content>
					</DropdownMenu.Root>
				</div>
			</div>
		</div>
	</div>
</nav>
