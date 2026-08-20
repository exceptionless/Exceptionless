<script module lang="ts">
    import { Html, Head, Body, Container, Section, Img, Preview } from '@better-svelte-email/components';

    // Preserve the legacy Foundation email geometry exactly. The fractional widths are
    // intentional browser table-layout results and are guarded by validate:parity.
    const clientStyles = `
:root{color-scheme:light only;supported-color-schemes:light}
html,body{margin:0!important;padding:0!important;width:100%!important;min-width:100%!important;background:#f7f7f7!important;color:#2c2c2c!important;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%}
table{border-collapse:collapse;border-spacing:0}
td{box-sizing:border-box}
img{border:0;line-height:100%;outline:none;text-decoration:none}
[data-email-header]>tbody>tr>td{padding-top:8px;padding-bottom:8px}
[data-email-content]>tbody>tr>td{padding:5px 16px 16px;-webkit-hyphens:auto;hyphens:auto;word-wrap:break-word}
[data-email-content][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"])>tbody>tr>td{padding-left:13px;padding-right:13px}
[data-email-actions]>tbody>tr>td{padding:0 16px 16px}
[data-email-actions][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"])>tbody>tr>td{padding-left:13px;padding-right:13px}
[data-email-actions] li,[data-email-actions] a{line-height:1.3}
[data-email-social]>tbody>tr>td{padding:0}
[data-email-social] a{line-height:1.3}
[data-email-social] table[role="presentation"] tr:not(:last-child)>td{padding-bottom:16px}
[data-email-button-row]{margin-bottom:21px}
[data-email-button-row] a span{line-height:1.3!important}
[data-email-content][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"]) [data-email-timeline-button]{margin-bottom:16px}
[data-email-content] [data-email-button-row]:last-child{margin-bottom:16px}
[data-email-unconfigured]{margin-top:21px!important}
[data-email-configure-button]{margin-top:15px}
[data-email-fields-card]{margin-bottom:21px!important}
[data-email-user-card]{margin-top:5px!important}
[data-email-fields-card]>tbody>tr>td,[data-email-user-card]>tbody>tr>td{padding:10px}
[data-email-summary-metrics]{margin-top:21px!important;margin-bottom:32px!important}
[data-email-summary-stat]{box-sizing:border-box;height:92px;padding-top:11px!important}
[data-email-summary-stat]>b{display:block;line-height:1.3;position:relative;top:-1px;text-align:left}
[data-email-summary-stat]>div{margin-top:-1px!important}
[data-email-summary-alert]{margin-top:1px!important;margin-bottom:21px!important;background-color:rgb(245,226,226)!important;border-color:rgb(111,39,37)!important}
[data-email-summary-alert]>tbody>tr>td{padding:10px}
[data-summary-count="3"] [data-summary-column]:nth-child(1){padding:0 6.828125px 0 0!important}
[data-summary-count="3"] [data-summary-column]:nth-child(2){padding:0 .65625px 0 9.171875px!important}
[data-summary-count="3"] [data-summary-column]:nth-child(3){padding:0 0 0 15.34375px!important}
[data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]{padding-left:10px!important;padding-right:10.5px!important}
[data-summary-count="3"] [data-summary-column]:nth-child(2) [data-email-summary-stat]{padding-left:9.5px!important;padding-right:9.6875px!important}
[data-summary-count="3"] [data-summary-column]:nth-child(3) [data-email-summary-stat]{padding-left:10.3125px!important;padding-right:10px!important}
[data-summary-count="4"]{margin-top:21px!important;margin-bottom:32px!important}
[data-summary-count="4"] [data-summary-column]:nth-child(1){padding:0 10.5px 0 0!important}
[data-summary-count="4"] [data-summary-column]:nth-child(2){padding:0 7px 0 5.5px!important}
[data-summary-count="4"] [data-summary-column]:nth-child(3){padding:0 4.5px 0 9px!important}
[data-summary-count="4"] [data-summary-column]:nth-child(4){padding:0 0 0 11.5px!important}
[data-summary-count="4"] [data-summary-column]:nth-child(1) [data-email-summary-stat]{padding-left:10px!important;padding-right:10.234375px!important}
[data-summary-count="4"] [data-summary-column]:nth-child(2) [data-email-summary-stat]{padding-left:9.765625px!important;padding-right:10.203125px!important}
[data-summary-count="4"] [data-summary-column]:nth-child(3) [data-email-summary-stat]{padding-left:9.796875px!important;padding-right:10.359375px!important}
[data-summary-count="4"] [data-summary-column]:nth-child(4) [data-email-summary-stat]{padding-left:9.640625px!important;padding-right:10px!important}
[data-email-free-plan]{margin-top:20.984375px!important}
[data-email-content] li,[data-email-content] li a{line-height:1.3}
p{margin:0 0 10px!important}
p+p{margin-top:15px!important}
@media only screen and (max-width:596px){[data-email-header]>tbody>tr>td{padding-left:15px;padding-right:15px}[data-email-header]>tbody>tr>td>table{width:95%!important}[data-email-container]{width:95%!important}[data-email-content][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"])>tbody>tr>td,[data-email-actions][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"])>tbody>tr>td{padding-left:16px;padding-right:16px}[data-social-column]{display:block!important;width:100%!important;max-width:100%!important;padding-left:16px!important;padding-right:16px!important}[data-social-column]:first-child{padding-bottom:32px!important}[data-email-summary-metrics]{position:relative;left:-16px;width:calc(100% + 32px)!important}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]{display:inline-block!important;width:33.33333%!important;padding:0 16px!important}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]{background-clip:padding-box!important;border-right-color:transparent!important;box-shadow:inset -1px 0 #cbcbcb;position:relative}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]::before,[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]::after{content:"";position:absolute;right:-1px;width:1px;height:1px;background:#f7f7f7}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]::before{top:-1px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]::after{bottom:-1px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(2) [data-email-summary-stat]>b{left:.5px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(2) [data-email-summary-stat]>div{position:relative;left:.09375px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(3) [data-email-summary-stat]>b{left:-.3125px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(3) [data-email-summary-stat]>div{position:relative;left:-.15625px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]{display:inline-block!important;width:25%!important;padding:0 16px!important}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(2) [data-email-summary-stat]{width:76.71875px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(2) [data-email-summary-stat]>b{left:.234375px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(3) [data-email-summary-stat]{width:57.5625px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(3) [data-email-summary-stat]>b{left:.203125px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(4) [data-email-summary-stat]{width:99.875px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(4) [data-email-summary-stat]>b{left:.359375px}[data-email-summary-stat]{min-width:max-content}[data-email-summary-stat]>div{white-space:nowrap}}
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
        <Section data-email-header class="bg-dark w-full py-2 px-4">
            <Container class="max-w-[580px] mx-auto">
                <Img
                    src="https://be.exceptionless.io/img/exceptionless-logo.png"
                    alt="Exceptionless"
                    width="235"
                    height="50"
                    class="block ml-[15px]"
                />
            </Container>
        </Section>
        <Container data-email-container class="max-w-[580px] mx-auto bg-bg">
            {@render content()}
        </Container>
    </Body>
</Html>
