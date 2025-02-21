<script lang="ts">
    import DarkModeButton from '$comp/dark-mode-button.svelte';
    import Logo from '$comp/logo.svelte';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Sidebar from '$comp/ui/sidebar';
    import logoSmall from '$lib/assets/exceptionless-48.png';
    import Search from 'lucide-svelte/icons/search';
    import { MediaQuery } from 'svelte/reactivity';

    interface Props {
        isCommandOpen: boolean;
    }

    let { isCommandOpen = $bindable() }: Props = $props();

    function onSearchClick(): void {
        isCommandOpen = true;
    }

    const isMediumScreenQuery = new MediaQuery('(min-width: 768px)');
</script>

<nav class="fixed z-30 w-full border-b bg-background text-foreground">
    <div class="px-3 py-3 lg:px-5 lg:pl-3">
        <div class="flex items-center justify-between">
            <div class="flex items-center justify-start">
                <Sidebar.Trigger variant="outline" class="size-9" />

                <a class="ml-2 mr-14 flex dark:text-white md:min-w-[250px] lg:ml-3" href="./">
                    {#if isMediumScreenQuery.current}
                        <Logo class="absolute top-[9px] mr-3 h-[45px]" />
                    {:else}
                        <img alt="Exceptionless Logo" class="mr-3 h-8" src={logoSmall} />
                    {/if}
                </a>
            </div>
            <div class="flex items-center gap-x-2 lg:gap-x-3">
                <Button onclick={onSearchClick} size="default" variant="outline">
                    <Search />
                    Search
                    <DropdownMenu.Shortcut class="ml-12">⌘K</DropdownMenu.Shortcut>
                </Button>

                <DarkModeButton></DarkModeButton>
            </div>
        </div>
    </div>
</nav>
