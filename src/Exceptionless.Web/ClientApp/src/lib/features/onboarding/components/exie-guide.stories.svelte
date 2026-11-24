<script module lang="ts">
    import { defineMeta } from '@storybook/addon-svelte-csf';

    import { Button } from '#comp/ui/button/index.ts';

    import ExieGuide from './exie-guide.svelte';
    import ExieMascot from './exie-mascot.svelte';
    import OnboardingStepper from './onboarding-stepper.svelte';

    const moods = ['hello', 'idle', 'listening', 'thinking', 'celebrate'] as const;

    const { Story } = defineMeta({
        argTypes: {
            layout: {
                control: {
                    type: 'select'
                },
                options: ['inline', 'stacked']
            },
            mood: {
                control: {
                    type: 'select'
                },
                options: moods
            }
        },
        component: ExieGuide,
        tags: ['autodocs'],
        title: 'Components/Onboarding/ExieGuide'
    });
</script>

<Story
    args={{
        layout: 'inline',
        mood: 'hello',
        size: 72,
        title: "Hi, I'm Exie!"
    }}
    name="Hello"
>
    {#snippet template(args)}
        <div class="max-w-md p-4">
            <ExieGuide layout={args.layout} mood={args.mood} size={args.size} title={args.title}
                >I'll help you catch your first error in about 5 minutes.</ExieGuide
            >
        </div>
    {/snippet}
</Story>

<Story
    args={{
        layout: 'inline',
        mood: 'listening',
        size: 64
    }}
    name="Listening with actions"
>
    {#snippet template(args)}
        <div class="max-w-lg p-4">
            <ExieGuide layout={args.layout} mood={args.mood} size={args.size} title={args.title}>
                Once the middleware is in, unhandled exceptions and 404s show up on their own.
                {#snippet actions()}
                    <Button size="sm">Copy snippet</Button>
                    <Button size="sm" variant="outline">Explore with sample data</Button>
                {/snippet}
            </ExieGuide>
        </div>
    {/snippet}
</Story>

<Story
    args={{
        layout: 'stacked',
        mood: 'celebrate',
        size: 96,
        title: 'You caught your first event!'
    }}
    name="Celebrate (stacked)"
>
    {#snippet template(args)}
        <div class="max-w-sm p-4">
            <ExieGuide layout={args.layout} mood={args.mood} size={args.size} title={args.title}>Your app is talking to Exceptionless.</ExieGuide>
        </div>
    {/snippet}
</Story>

<Story name="All moods">
    {#snippet template()}
        <div class="flex flex-wrap items-end gap-8 p-4">
            {#each moods as mood (mood)}
                <figure class="flex flex-col items-center gap-2">
                    <ExieMascot {mood} size={96} />
                    <figcaption class="text-muted-foreground text-xs">{mood}</figcaption>
                </figure>
            {/each}
        </div>
    {/snippet}
</Story>

<Story name="Narrow (360px)">
    {#snippet template()}
        <div class="flex w-[360px] flex-col gap-4 p-4">
            <ExieGuide mood="hello" size={48}>Hi, I'm Exie! Let's catch your first error in about 5 minutes.</ExieGuide>
            <OnboardingStepper current="install-client" />
        </div>
    {/snippet}
</Story>
