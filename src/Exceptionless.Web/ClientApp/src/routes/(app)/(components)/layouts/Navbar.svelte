<script lang="ts">
    import DarkModeButton from '$comp/DarkModeButton.svelte';
    import Loading from '$comp/Loading.svelte';
    import { A } from '$comp/typography';
    import * as Avatar from '$comp/ui/avatar';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Sidebar from '$comp/ui/sidebar';
    import { getGravatarFromCurrentUser } from '$features/users/gravatar.svelte';
    import logoSmall from '$lib/assets/exceptionless-48.png';
    import logo from '$lib/assets/logo.svg';
    import logoDark from '$lib/assets/logo-dark.svg';
    import IconSearch from '~icons/mdi/search';
    import { MediaQuery } from 'runed';

    interface Props {
        isCommandOpen: boolean;
    }

    let { isCommandOpen = $bindable() }: Props = $props();

    function onSearchClick(): void {
        isCommandOpen = true;
    }

    const gravatar = getGravatarFromCurrentUser();
    const isMediumScreenQuery = new MediaQuery('(min-width: 768px)');
</script>

<nav class="fixed z-30 w-full border-b bg-background text-foreground">
    <div class="px-3 py-3 lg:px-5 lg:pl-3">
        <div class="flex items-center justify-between">
            <div class="flex items-center justify-start">
                <Sidebar.Trigger variant="outline" class="size-9" />

                <a class="ml-2 mr-14 flex min-w-[250px] dark:text-white lg:ml-3" href="./">
                    {#if isMediumScreenQuery.matches}
                        <img alt="Exceptionless Logo" class="absolute top-[0px] mr-3 h-[65px] dark:hidden" src={logo} />
                        <img alt="Exceptionless Logo" class="absolute top-[0px] mr-3 hidden h-[65px] dark:block" src={logoDark} />
                    {:else}
                        <img alt="Exceptionless Logo" class="mr-3 h-8" src={logoSmall} />
                    {/if}
                </a>
            </div>
            <div class="flex items-center gap-x-2 lg:gap-x-3">
                <Button onclick={onSearchClick} size="default" variant="outline">
                    <IconSearch />
                    Search
                    <DropdownMenu.Shortcut class="ml-12">⌘K</DropdownMenu.Shortcut>
                </Button>

                <DarkModeButton></DarkModeButton>

                <DropdownMenu.Root>
                    <DropdownMenu.Trigger>
                        {#snippet children()}
                            <Button class="rounded-full" size="icon" variant="ghost">
                                <Avatar.Root class="size-7" title="Profile Image">
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
                            <DropdownMenu.Item
                                ><A variant="ghost" href="/next/account/manage" class="w-full">Account</A>
                                <DropdownMenu.Shortcut>⇧⌘P</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                            <!-- <DropdownMenu.Item
                                ><A variant="ghost" href="/next/account/notifications" class="w-full">Notifications</A>
                                <DropdownMenu.Shortcut>⇧⌘N</DropdownMenu.Shortcut>
                            </DropdownMenu.Item> -->
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
                            <DropdownMenu.Item><A variant="ghost" href="/account/manage" class="w-full">
                                Billing</A>
                                <DropdownMenu.Shortcut>⌘B</DropdownMenu.Shortcut>
                            </DropdownMenu.Item>
                        </DropdownMenu.Group> -->
                        <DropdownMenu.Label>Documentation</DropdownMenu.Label>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Group>
                            <DropdownMenu.Item
                                ><A variant="ghost" href="https://exceptionless.com/docs/" target="_blank" class="w-full">Documentation</A></DropdownMenu.Item
                            >
                            <DropdownMenu.Item
                                ><A variant="ghost" href="https://github.com/exceptionless/Exceptionless/issues" target="_blank" class="w-full">Support</A
                                ></DropdownMenu.Item
                            >
                            <DropdownMenu.Item
                                ><A variant="ghost" href="https://github.com/exceptionless/Exceptionless" target="_blank" class="w-full">GitHub</A
                                ></DropdownMenu.Item
                            >
                            <DropdownMenu.Item><A variant="ghost" href="/docs/index.html" target="_blank" class="w-full">API</A></DropdownMenu.Item>
                            <!-- <DropdownMenu.Item>
                            Keyboard shortcuts
                            <DropdownMenu.Shortcut>⌘K</DropdownMenu.Shortcut>
                        </DropdownMenu.Item> -->
                        </DropdownMenu.Group>
                        <DropdownMenu.Separator />
                        <DropdownMenu.Item
                            ><A variant="ghost" href="/next/logout" class="w-full">Sign out</A>
                            <DropdownMenu.Shortcut>⇧⌘Q</DropdownMenu.Shortcut>
                        </DropdownMenu.Item>
                    </DropdownMenu.Content>
                </DropdownMenu.Root>
            </div>
        </div>
    </div>
</nav>
