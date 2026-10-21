<script lang="ts">
    import CopyToClipboardButton from '#comp/copy-to-clipboard-button.svelte';
    import { A, CodeBlock } from '#comp/typography/index.ts';
    import { Badge } from '#comp/ui/badge/index.ts';

    import type { Platform, PlatformSetupContext, PlatformStep } from '../platforms';

    interface Props {
        context: PlatformSetupContext;
        platform: Platform;
    }

    let { context, platform }: Props = $props();

    const steps = $derived(platform.steps(context));
    const requiredSteps = $derived(steps.filter((step) => !step.optional));
    const optionalSteps = $derived(steps.filter((step) => step.optional));
</script>

{#snippet stepContent(step: PlatformStep)}
    <div class="flex min-w-0 flex-col gap-2">
        <h3 class="text-foreground flex items-center gap-2 text-sm font-semibold">
            {step.title}
            {#if step.optional}<Badge variant="secondary">Optional</Badge>{/if}
        </h3>
        {#if step.description}
            <p class="text-muted-foreground text-sm">{step.description}</p>
        {/if}
        {#each step.blocks ?? [] as block, index (index)}
            <div class="bg-muted relative min-h-13 overflow-hidden rounded-md">
                <CodeBlock class="overflow-x-auto pr-12" code={block.code} language={block.language} />
                <div class="absolute top-2 right-2">
                    <CopyToClipboardButton size="icon-sm" title="Copy code" value={block.code} variant="outline" />
                </div>
            </div>
        {/each}
    </div>
{/snippet}

<section aria-labelledby="platform-instructions-heading" class="flex flex-col gap-4" data-tour="project-sdk-instructions">
    <div class="flex flex-wrap items-baseline justify-between gap-2">
        <h2 class="text-foreground text-base font-semibold" id="platform-instructions-heading">Connect {platform.label}</h2>
        <A class="text-sm" href={platform.docsUrl} target="_blank">{platform.isRawHttp ? 'Event API docs' : `${platform.category} client docs`}</A>
    </div>

    <ol class="flex flex-col gap-5">
        {#each requiredSteps as step, index (step.title)}
            <li class="flex gap-3">
                <span
                    aria-hidden="true"
                    class="border-border text-muted-foreground mt-0.5 flex size-6 shrink-0 items-center justify-center rounded-full border text-xs font-semibold"
                    >{index + 1}</span
                >
                {@render stepContent(step)}
            </li>
        {/each}
    </ol>

    {#if optionalSteps.length > 0}
        <div class="border-border flex flex-col gap-4 border-t pt-4 pl-9">
            {#each optionalSteps as step (step.title)}
                {@render stepContent(step)}
            {/each}
        </div>
    {/if}
</section>
