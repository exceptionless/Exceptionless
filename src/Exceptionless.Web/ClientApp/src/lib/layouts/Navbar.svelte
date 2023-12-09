<script lang="ts">
	import IconClose from '~icons/mdi/close';
	import IconMenu from '~icons/mdi/menu';
	import IconSearch from '~icons/mdi/search';

	import logo from '$lib/assets/exceptionless-48.png';
	import { isSidebarOpen } from '$lib/stores/sidebar';
	import * as Avatar from '$comp/ui/avatar';
	import * as DropdownMenu from '$comp/ui/dropdown-menu';
	import SearchInput from '$comp/SearchInput.svelte';
	import DarkModeButton from '$comp/DarkModeButton.svelte';
	import { Button } from '$comp/ui/button';

	let filter = '';

	function onHamburgerClick(): void {
		isSidebarOpen.set(!$isSidebarOpen);
	}
</script>

<nav class="fixed z-30 w-full border-b bg-background text-foreground">
	<div class="px-3 py-3 lg:px-5 lg:pl-3">
		<div class="flex items-center justify-between">
			<div class="flex items-center justify-start">
				<Button
					on:click={onHamburgerClick}
					variant="outline"
					size="icon"
					class="hidden p-1 mr-3 lg:block"
					aria-controls="sidebar"
				>
					<IconMenu class="w-6 h-6" />
				</Button>
				<Button
					on:click={onHamburgerClick}
					variant="outline"
					size="icon"
					class="mr-2 lg:hidden"
					aria-controls="sidebar"
				>
					{#if $isSidebarOpen}
						<IconClose class="w-6 h-6" />
					{:else}
						<IconMenu class="w-6 h-6" />
					{/if}
				</Button>
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
			<div class="flex items-center gap-x-2 lg:gap-x-3">
				<Button
					variant="outline"
					size="icon"
					on:click={() => isSidebarOpen.set(!$isSidebarOpen)}
					class="lg:hidden"
				>
					<span class="sr-only">Search</span>

					<IconSearch class="w-6 h-6" />
				</Button>

				<DarkModeButton></DarkModeButton>

				<DropdownMenu.Root>
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
					<DropdownMenu.Content class="w-56" align="end">
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
</nav>
