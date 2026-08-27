<script module lang="ts">
    import { Html, Head, Body, Container, Section, Img, Preview } from '@better-svelte-email/components';

    const clientStyles = `
:root{color-scheme:light only;supported-color-schemes:light}
html,body{margin:0!important;padding:0!important;width:100%!important;min-width:100%!important;background:#f7f7f7!important;color:#2c2c2c!important;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%}
table{border-collapse:collapse;border-spacing:0}
td{box-sizing:border-box}
img{border:0;line-height:100%;outline:none;text-decoration:none}
@media only screen and (max-width:596px){[data-email-container]{width:95%!important}[data-social-column]{display:block!important;width:100%!important;max-width:100%!important}[data-summary-column]{display:inline-block!important;width:50%!important}}
`;
</script>

<script lang="ts">
    import type { Snippet } from 'svelte';
    let { content, preheader = '' }: { content: Snippet; preheader?: string } = $props();
</script>

<Html lang="en">
    <Head>
        <title>{'{{Subject}}'}</title>
        <meta name="color-scheme" content="light only" />
        <meta name="supported-color-schemes" content="light only" />
        <svelte:element this={"style"}>{clientStyles}</svelte:element>
    </Head>
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
        <Container data-email-container class="max-w-[580px] mx-auto bg-bg">
            {@render content()}
        </Container>
    </Body>
</Html>
