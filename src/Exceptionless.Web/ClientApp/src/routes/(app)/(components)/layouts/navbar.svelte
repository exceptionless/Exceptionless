<script lang="ts">
    import { resolve } from '$app/paths';
    import Search from '@lucide/svelte/icons/search';
    import Sparkles from '@lucide/svelte/icons/sparkles';
    import { MediaQuery } from 'svelte/reactivity';

    import DarkModeButton from '#comp/dark-mode-button.svelte';
    import Logo from '#comp/logo.svelte';
    import { A } from '#comp/typography/index.ts';
    import { Button } from '#comp/ui/button/index.ts';
    import * as Kbd from '#comp/ui/kbd/index.ts';
    import * as Sidebar from '#comp/ui/sidebar/index.ts';
    import logoSmall from '#lib/assets/exceptionless-48.png';

    interface Props {
        assistantEnabled: boolean;
        isAssistantOpen: boolean;
        openCommand: () => void;
        toggleAssistant: () => void;
    }

    let { assistantEnabled, isAssistantOpen, openCommand, toggleAssistant }: Props = $props();

    const isMediumScreenQuery = new MediaQuery('(min-width: 768px)');
</script>

<nav class="bg-background text-foreground fixed z-30 w-full border-b">
    <div class="px-4 py-3">
        <div class="flex items-center justify-between">
            <div class="flex items-center justify-start">
                <Sidebar.Trigger variant="outline" class="size-9" />

                <A variant="ghost" class="mr-14 ml-2 flex md:min-w-62.5 lg:ml-3 dark:text-white" href={resolve('/(app)')}>
                    {#if isMediumScreenQuery.current}
                        <Logo class="absolute top-1.5 mr-3 h-12" />
                    {:else}
                        <img alt="Exceptionless Logo" class="mr-3 h-8" src={logoSmall} />
                    {/if}
                </A>
            </div>
            <div class="flex items-center gap-2">
                <Button
                    aria-label="Search Exceptionless"
                    class="w-9 justify-center sm:w-56 sm:justify-start md:w-72"
                    data-tour="command-search"
                    onclick={openCommand}
                    size="default"
                    variant="outline"
                >
                    <Search />
                    <span class="text-muted-foreground hidden items-center gap-1.5 sm:flex">Type <Kbd.Root>/</Kbd.Root> to search</span>
                </Button>

                {#if assistantEnabled}
                    <Button
                        aria-expanded={isAssistantOpen}
                        aria-keyshortcuts="A"
                        aria-label={isAssistantOpen ? 'Close Exie' : 'Open Exie'}
                        class="px-2"
                        data-assistant-trigger
                        data-tour="exie-trigger"
                        onclick={toggleAssistant}
                        title={isAssistantOpen ? 'Close Exie (A)' : 'Ask Exie (A)'}
                        variant={isAssistantOpen ? 'secondary' : 'outline'}
                    >
                        <Sparkles aria-hidden="true" class="text-primary size-5" data-icon="inline-start" />
                        <span class="hidden lg:inline">{isAssistantOpen ? 'Close Exie' : 'Ask Exie'}</span>
                    </Button>
                {/if}

                <DarkModeButton></DarkModeButton>
            </div>
        </div>
    </div>
</nav>
