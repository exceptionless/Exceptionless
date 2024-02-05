<script lang="ts">
    import IconClose from '~icons/mdi/close';
    import IconMenu from '~icons/mdi/menu';
    import IconSearch from '~icons/mdi/search';

    import logo from '$lib/assets/logo.svg';
    import logoDark from '$lib/assets/logo-dark.svg';
    import logoSmall from '$lib/assets/exceptionless-48.png';
    import { isLargeScreen, isSidebarOpen } from '$lib/stores/sidebar';
    import * as Avatar from '$comp/ui/avatar';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import SearchInput from '$comp/SearchInput.svelte';
    import DarkModeButton from '$comp/DarkModeButton.svelte';
    import { Button } from '$comp/ui/button';
    import { getGravatarFromCurrentUserSrc } from '$api/gravatar';
    let filter = '';

    function onHamburgerClick(): void {
        isSidebarOpen.set(!$isSidebarOpen);
    }

    const gravatarSrc = getGravatarFromCurrentUserSrc();
</script>

<nav class="fixed z-30 w-full border-b bg-background text-foreground">
    <div class="px-3 py-3 lg:px-5 lg:pl-3">
        <div class="flex items-center justify-between">
            <div class="flex items-center justify-start">
                <Button on:click={onHamburgerClick} variant="outline" size="icon" class="mr-3 hidden p-1 lg:block" aria-controls="sidebar">
                    <IconMenu class="h-6 w-6" />
                </Button>
                <Button on:click={onHamburgerClick} variant="outline" size="icon" class="mr-2 lg:hidden" aria-controls="sidebar">
                    {#if $isSidebarOpen}
                        <IconClose class="h-6 w-6" />
                    {:else}
                        <IconMenu class="h-6 w-6" />
                    {/if}
                </Button>
                <a href="./" class="mr-14 flex min-w-[250px] dark:text-white">
                    {#if $isLargeScreen}
                        <img src={logo} class="absolute top-[1px] mr-3 h-[67px] dark:hidden" alt="Exceptionless Logo" />
                        <img src={logoDark} class="absolute top-[1px] mr-3 hidden h-[67px] dark:block" alt="Exceptionless Logo" />
                    {:else}
                        <img src={logoSmall} class="mr-3 h-8" alt="Exceptionless Logo" />
                    {/if}
                </a>
            </div>
            <form action="/" method="GET" class="hidden lg:block lg:pl-2">
                <div class="mt-1 w-[350px] xl:w-[550px]">
                    <SearchInput id="topbar-search" value={filter} />
                </div>
            </form>
            <div class="flex items-center gap-x-2 lg:gap-x-3">
                <Button variant="outline" size="icon" on:click={() => isSidebarOpen.set(!$isSidebarOpen)} class="lg:hidden">
                    <span class="sr-only">Search</span>

                    <IconSearch class="h-6 w-6" />
                </Button>

                <DarkModeButton></DarkModeButton>

                <DropdownMenu.Root>
                    <DropdownMenu.Trigger asChild let:builder>
                        <Button builders={[builder]} size="icon" variant="ghost" class="rounded-full">
                            <Avatar.Root title="Profile Image" class="h-7 w-7">
                                {#await $gravatarSrc}
                                    <Avatar.Fallback><Loading /></Avatar.Fallback>
                                {:then src}
                                    <Avatar.Image {src} alt="gravatar" />
                                {:catch}
                                    <Avatar.Fallback>Error</Avatar.Fallback>
                                {/await}
                            </Avatar.Root>
                        </Button>
                    </DropdownMenu.Trigger>
                    <DropdownMenu.Content class="w-56" align="end">
                        <DropdownMenu.Label>My Account</DropdownMenu.Label>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Group>
                            <DropdownMenu.Item href="/next/account/manage">
                                Profile
                                <DropdownMenu.Shortcut>⇧⌘P</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                        </DropdownMenu.Group>
                        <DropdownMenu.Label>My Organization</DropdownMenu.Label>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Group>
                            <DropdownMenu.Item>
                                Settings
                                <DropdownMenu.Shortcut>⌘S</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item>Team</DropdownMenu.Item>
                            <DropdownMenu.Item>
                                Invite users
                                <DropdownMenu.Shortcut>⌘+I</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item href="/account/manage">
                                Billing
                                <DropdownMenu.Shortcut>⌘B</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                        </DropdownMenu.Group>
                        <DropdownMenu.Label>Documentation</DropdownMenu.Label>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Item href="https://exceptionless.com/docs/" target="_blank">Documentation</DropdownMenu.Item>
                        <DropdownMenu.Item href="https://github.com/exceptionless/Exceptionless/issues" target="_blank">Support</DropdownMenu.Item>
                        <DropdownMenu.Item href="https://github.com/exceptionless/Exceptionless" target="_blank">GitHub</DropdownMenu.Item>
                        <DropdownMenu.Item href="/docs/index.html" target="_blank">API</DropdownMenu.Item>
                        <DropdownMenu.Item>
                            Keyboard shortcuts
                            <DropdownMenu.Shortcut>⌘K</DropdownMenu.Shortcut>
                        </DropdownMenu.Item>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Item href="/next/logout">
                            Sign out
                            <DropdownMenu.Shortcut>⇧⌘Q</DropdownMenu.Shortcut>
                        </DropdownMenu.Item>
                    </DropdownMenu.Content>
                </DropdownMenu.Root>
            </div>
        </div>
    </div>
</nav>
