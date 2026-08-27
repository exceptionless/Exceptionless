<script module lang="ts">
    import { Body, Container, Head, Html, Img, Preview, Section } from '@better-svelte-email/components';

    const sharedStyles = `
:root{color-scheme:light only;supported-color-schemes:light}
html,body{margin:0!important;padding:0!important;width:100%!important;min-width:100%!important;background:#fff!important;color:#111827!important;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%}
table{border-collapse:collapse;border-spacing:0}
td{box-sizing:border-box}
img{border:0;line-height:100%;outline:none;text-decoration:none}
[data-email-header]>tbody>tr>td{padding-top:8px;padding-bottom:8px}
[data-email-content]>tbody>tr>td{padding:5px 16px 16px;-webkit-hyphens:auto;hyphens:auto;word-wrap:break-word}
[data-email-button-row]{margin-bottom:21px}
[data-email-button-row] a span{line-height:1.3!important}
[data-email-content] [data-email-button-row]:last-child{margin-bottom:16px}
p{margin:0 0 10px!important}
p+p{margin-top:15px!important}
@media only screen and (max-width:596px){[data-email-header]>tbody>tr>td{padding-left:15px;padding-right:15px}[data-email-header]>tbody>tr>td>table{width:95%!important}[data-email-container]{width:95%!important}}
`;
</script>

<script lang="ts">
    import type { Snippet } from 'svelte';

    let { content, preheader = '', styles = '' }: { content: Snippet; preheader?: string; styles?: string } = $props();
</script>

<Html lang="en">
    <Head>
        <title>{'{{Subject}}'}</title>
        <meta name="color-scheme" content="light only" />
        <meta name="supported-color-schemes" content="light only" />
        <svelte:element this={"style"}>{sharedStyles}{styles}</svelte:element>
    </Head>
    <Body class="bg-background text-foreground font-[Helvetica,Arial,sans-serif]">
        {#if preheader}
            <Preview preview={preheader} />
        {/if}
        <Section data-email-header class="bg-foreground w-full px-4 py-2">
            <Container class="mx-auto max-w-[580px]">
                <Img src="https://be.exceptionless.io/img/exceptionless-logo.png" alt="Exceptionless" width="235" height="50" class="ml-[15px] block" />
            </Container>
        </Section>
        <Container data-email-container class="bg-background mx-auto max-w-[580px]">
            {@render content()}
        </Container>
    </Body>
</Html>
