<script module lang="ts">
    import { Html, Head, Body, Container, Section, Img } from '@better-svelte-email/components';
</script>

<script lang="ts">
    import type { Snippet } from 'svelte';
    let { content, preheader = '' }: { content: Snippet; preheader?: string } = $props();
</script>

<Html lang="en">
    <Head />
    <Body class="bg-bg font-[Helvetica,Arial,sans-serif]">
        {#if preheader}
            <span
                class="hidden max-h-0 max-w-0 overflow-hidden opacity-0"
                style="display:none!important;font-size:1px;line-height:1px;max-height:0;max-width:0;mso-hide:all!important;opacity:0;overflow:hidden;visibility:hidden"
                >{preheader}</span
            >
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
        <div style="display:none;white-space:nowrap;font:15px courier;line-height:0">
            &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;
            &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;
        </div>
    </Body>
</Html>
