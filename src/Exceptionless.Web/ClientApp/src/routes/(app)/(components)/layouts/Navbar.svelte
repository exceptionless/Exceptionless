<script lang="ts">
    import DarkModeButton from '$comp/DarkModeButton.svelte';
    import Loading from '$comp/Loading.svelte';
    import * as Avatar from '$comp/ui/avatar';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import logoSmall from '$lib/assets/exceptionless-48.png';
    import logo from '$lib/assets/logo.svg';
    import logoDark from '$lib/assets/logo-dark.svg';
    import IconClose from '~icons/mdi/close';
    import IconMenu from '~icons/mdi/menu';
    import IconSearch from '~icons/mdi/search';

    interface Props {
        isCommandOpen: boolean;
        isMediumScreen?: boolean;
        isSidebarOpen: boolean;
    }

    let { isCommandOpen = $bindable(), isMediumScreen, isSidebarOpen = $bindable() }: Props = $props();

    function onHamburgerClick(): void {
        isSidebarOpen = !isSidebarOpen;
    }

    function onSearchClick(): void {
        isCommandOpen = true;
    }

    const gravatar = getGravatarFromCurrentUser();
</script>

<nav class="fixed z-30 w-full border-b bg-background text-foreground">
    <div class="px-3 py-3 lg:px-5 lg:pl-3">
        <div class="flex items-center justify-between">
            <div class="flex items-center justify-start">
                <Button aria-controls="sidebar" class="mr-3 hidden p-1 lg:block" onclick={onHamburgerClick} size="icon" variant="outline">
                    <IconMenu class="h-6 w-6" />
                </Button>
                <Button aria-controls="sidebar" class="mr-2 lg:hidden" onclick={onHamburgerClick} size="icon" variant="outline">
                    {#if isSidebarOpen}
                        <IconClose class="h-6 w-6" />
                    {:else}
                        <IconMenu class="h-6 w-6" />
                    {/if}
                </Button>
                <a class="mr-14 flex min-w-[250px] dark:text-white" href="./">
                    {#if isMediumScreen}
                        <img alt="Exceptionless Logo" class="absolute top-[0px] mr-3 h-[65px] dark:hidden" src={logo} />
                        <img alt="Exceptionless Logo" class="absolute top-[0px] mr-3 hidden h-[65px] dark:block" src={logoDark} />
                    {:else}
                        <img alt="Exceptionless Logo" class="mr-3 h-8" src={logoSmall} />
                    {/if}
                </a>
            </div>
            <div class="flex items-center gap-x-2 lg:gap-x-3">
                <Button onclick={onSearchClick} size="default" variant="outline">
                    <IconSearch class="h-6 w-6" />
                    Search
                    <DropdownMenu.Shortcut class="ml-12">⌘K</DropdownMenu.Shortcut>
                </Button>

                <DarkModeButton></DarkModeButton>

                <DropdownMenu.Root>
                    <DropdownMenu.Trigger asChild>
                        {#snippet children({ builder })}
                            <Button builders={[builder]} class="rounded-full" size="icon" variant="ghost">
                                <Avatar.Root class="h-7 w-7" title="Profile Image">
                                    {#await gravatar.src}
                                        <Avatar.Fallback><Loading /></Avatar.Fallback>
                                    {:then src}
                                        <Avatar.Image alt="gravatar" {src} />
                                    {/await}
                                    <Avatar.Fallback>{gravatar.initials}</Avatar.Fallback>
                                </Avatar.Root>
                            </Button>
                        {/snippet}
                    </DropdownMenu.Trigger>
                    <DropdownMenu.Content align="end" class="w-56">
                        <DropdownMenu.Label>My Account</DropdownMenu.Label>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Group>
                            <DropdownMenu.Item href="/next/account/manage">
                                Account
                                <DropdownMenu.Shortcut>⇧⌘P</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                            <DropdownMenu.Item href="/next/account/notifications">
                                Notifications
                                <DropdownMenu.Shortcut>⇧⌘N</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                        </DropdownMenu.Group>
                        <!-- <DropdownMenu.Label>My Organization</DropdownMenu.Label>
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
                        </DropdownMenu.Group> -->
                        <DropdownMenu.Label>Documentation</DropdownMenu.Label>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Group>
                            <DropdownMenu.Item href="https://exceptionless.com/docs/" target="_blank">Documentation</DropdownMenu.Item>
                            <DropdownMenu.Item href="https://github.com/exceptionless/Exceptionless/issues" target="_blank">Support</DropdownMenu.Item>
                            <DropdownMenu.Item href="https://github.com/exceptionless/Exceptionless" target="_blank">GitHub</DropdownMenu.Item>
                            <DropdownMenu.Item href="/docs/index.html" target="_blank">API</DropdownMenu.Item>
                            <!-- <DropdownMenu.Item>
                            Keyboard shortcuts
                            <DropdownMenu.Shortcut>⌘K</DropdownMenu.Shortcut>
                        </DropdownMenu.Item> -->
                        </DropdownMenu.Group>
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
