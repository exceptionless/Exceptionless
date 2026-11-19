<script lang="ts">
    import ArrowRight from '@lucide/svelte/icons/arrow-right';

    import { Button } from '#comp/ui/button/index.ts';
    import * as Card from '#comp/ui/card/index.ts';

    import ExieMascot from './exie-mascot.svelte';
    import SampleDataButton from './sample-data-button.svelte';

    interface Props {
        continueSetupHref: string;
        /** The project sample data goes to; without one, only setup is offered. */
        sampleProject?: { id: string; name: string };
    }

    let { continueSetupHref, sampleProject }: Props = $props();
</script>

<Card.Root class="items-center px-6 py-10 text-center" data-exie-empty-state>
    <ExieMascot mood="listening" size={88} />
    <div class="flex max-w-md flex-col gap-2">
        <h2 class="text-foreground text-lg font-semibold">Nothing yet — I'm listening!</h2>
        <p class="text-muted-foreground text-sm">Connect your app and events will show up here in real time.</p>
    </div>
    <div class="flex flex-wrap justify-center gap-2">
        <Button href={continueSetupHref}>Continue setup <ArrowRight data-icon="inline-end" aria-hidden="true" /></Button>
        {#if sampleProject}
            <SampleDataButton projectId={sampleProject.id} projectName={sampleProject.name} />
        {/if}
    </div>
</Card.Root>
