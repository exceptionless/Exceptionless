<script lang="ts">
    import { resolve } from '$app/paths';
    import DarkModeButton from '$comp/dark-mode-button.svelte';
    import Logo from '$comp/logo.svelte';
    import { A } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Kbd from '$comp/ui/kbd';
    import * as Sidebar from '$comp/ui/sidebar';
    import logoSmall from '$lib/assets/exceptionless-48.png';
    import Search from '@lucide/svelte/icons/search';
    import { MediaQuery } from 'svelte/reactivity';

    interface Props {
        openCommand: () => void;
    }

    let { openCommand }: Props = $props();

    const isMediumScreenQuery = new MediaQuery('(min-width: 768px)');
</script>

<nav class="fixed z-30 w-full border-b bg-background text-foreground">
    <div class="px-4 py-3">
        <div class="flex items-center justify-between">
            <div class="flex items-center justify-start">
                <Sidebar.Trigger variant="outline" class="size-9" />

                <A variant="ghost" class="mr-14 ml-2 flex md:min-w-62.5 lg:ml-3 dark:text-white" href={resolve('/(app)/stack')}>
                    {#if isMediumScreenQuery.current}
                        <Logo class="absolute top-1 mr-3 h-14" />
                    {:else}
                        <img alt="Exceptionless Logo" class="mr-3 h-8" src={logoSmall} />
                    {/if}
                </A>
            </div>
            <div class="flex items-center gap-2">
                <Button class="w-44 justify-start sm:w-56 md:w-72" onclick={openCommand} size="default" variant="outline">
                    <Search />
                    <span class="flex items-center gap-1.5 text-muted-foreground">Type <Kbd.Root>/</Kbd.Root> to search</span>
                </Button>

                <DarkModeButton></DarkModeButton>
            </div>
        </div>
    </div>
</nav>
