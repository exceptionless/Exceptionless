<script lang="ts">
    import ArrowUpRight from '@lucide/svelte/icons/arrow-up-right';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import Copy from '@lucide/svelte/icons/copy';
    import { toast } from 'svelte-sonner';

    import { Button } from '#comp/ui/button/index.ts';
    import * as DropdownMenu from '#comp/ui/dropdown-menu/index.ts';
    import { UseClipboard } from '#lib/hooks/use-clipboard.svelte.ts';

    import type { Platform, PlatformSetupContext } from '../platforms';

    import { AGENT_TARGETS, buildAgentSetupPrompt, getAgentUrl } from '../agent-setup';
    import AgentIcon from './agent-icon.svelte';

    interface Props {
        context: PlatformSetupContext;
        /** Without a platform, the prompt asks the agent to work out the app's stack. */
        platform?: Platform;
    }

    let { context, platform }: Props = $props();

    const prompt = $derived(buildAgentSetupPrompt(context, platform));
    const targets = $derived(
        AGENT_TARGETS.map((target) => ({
            ...target,
            url: getAgentUrl(target.id, prompt)
        }))
    );
    const clipboard = new UseClipboard();

    async function copyPrompt(): Promise<void> {
        await clipboard.copy(prompt);
        if (clipboard.copied) {
            toast.success('Setup prompt copied. Paste it into your coding agent.');
        } else {
            toast.error('Unable to copy the setup prompt.');
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} data-agent-setup-menu size="sm" variant="outline">
                Set up with your agent
                <ChevronDown data-icon="inline-end" aria-hidden="true" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end" class="w-64">
        <DropdownMenu.Item class="items-start gap-3 py-2" onclick={copyPrompt}>
            <span class="bg-muted flex size-8 shrink-0 items-center justify-center rounded-md"><Copy aria-hidden="true" class="size-4" /></span>
            <span class="flex flex-col">
                <span class="text-foreground font-medium">Copy setup prompt</span>
                <span class="text-muted-foreground text-xs">Paste into your coding agent</span>
            </span>
        </DropdownMenu.Item>
        <DropdownMenu.Separator />
        <DropdownMenu.Group>
            <DropdownMenu.GroupHeading>Open in</DropdownMenu.GroupHeading>
            {#each targets as target (target.id)}
                <DropdownMenu.Item class="gap-3">
                    {#snippet child({ props })}
                        <a {...props} href={target.url} rel="noopener noreferrer" target="_blank">
                            <span class="bg-muted flex size-8 shrink-0 items-center justify-center rounded-md">
                                <AgentIcon class="size-4" target={target.id} />
                            </span>
                            <span class="text-foreground flex-1 font-medium">{target.label}</span>
                            <ArrowUpRight aria-hidden="true" class="text-muted-foreground size-4" />
                            <span class="sr-only">(opens in a new tab)</span>
                        </a>
                    {/snippet}
                </DropdownMenu.Item>
            {/each}
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>
