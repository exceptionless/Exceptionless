<script module lang="ts">
    import { Html, Head, Body, Container, Section, Img, Preview } from '@better-svelte-email/components';
</script>

<script lang="ts">
    import type { Snippet } from 'svelte';
    let { content, preheader = '' }: { content: Snippet; preheader?: string } = $props();
</script>

<Html lang="en">
    <Head />
    <Body class="bg-bg font-[Helvetica,Arial,sans-serif]">
        {#if preheader}
            <Preview preview={preheader} />
        {/if}
        <Section class="bg-dark w-full py-2 px-4">
            <Container class="max-w-[580px] mx-auto">
                <Img
                    src="https://be.exceptionless.io/img/exceptionless-logo.png"
                    alt="Exceptionless"
                    width="266"
                    height="50"
                    class="block ml-4"
                />
            </Container>
        </Section>
        <Container class="max-w-[580px] mx-auto bg-bg">
            {@render content()}
        </Container>
    </Body>
</Html>
