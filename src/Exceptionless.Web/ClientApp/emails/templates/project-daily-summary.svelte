<script module lang="ts">
    import { Button, Column, Heading, Link, Row, Section, Text } from '@better-svelte-email/components';

    import ActionsFooter, { actionsFooterStyles } from '../components/ActionsFooter.svelte';
    import EmailLayout from '../components/EmailLayout.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';

    const dailySummaryStyles = `${actionsFooterStyles}
[data-email-content][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"])>tbody>tr>td{padding-left:13px;padding-right:13px}
[data-email-actions][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"])>tbody>tr>td{padding-left:13px;padding-right:13px}
[data-email-content][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"]) [data-email-timeline-button]{margin-bottom:16px}
[data-email-unconfigured]{margin-top:21px!important}
[data-email-configure-button]{margin-top:15px}
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
@media only screen and (max-width:596px){[data-email-content][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"])>tbody>tr>td,[data-email-actions][data-summary-blocked]:not([data-summary-blocked=""]):not([data-summary-blocked="0"])>tbody>tr>td{padding-left:16px;padding-right:16px}[data-email-summary-metrics]{position:relative;left:-16px;width:calc(100% + 32px)!important}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]{display:inline-block!important;width:33.33333%!important;padding:0 16px!important}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]{background-clip:padding-box!important;border-right-color:transparent!important;box-shadow:inset -1px 0 #cbcbcb;position:relative}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]::before,[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]::after{content:"";position:absolute;right:-1px;width:1px;height:1px;background:#f7f7f7}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]::before{top:-1px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(1) [data-email-summary-stat]::after{bottom:-1px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(2) [data-email-summary-stat]>b{left:.5px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(2) [data-email-summary-stat]>div{position:relative;left:.09375px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(3) [data-email-summary-stat]>b{left:-.3125px}[data-email-summary-metrics][data-summary-count="3"] [data-summary-column]:nth-child(3) [data-email-summary-stat]>div{position:relative;left:-.15625px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]{display:inline-block!important;width:25%!important;padding:0 16px!important}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(2) [data-email-summary-stat]{width:76.71875px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(2) [data-email-summary-stat]>b{left:.234375px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(3) [data-email-summary-stat]{width:57.5625px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(3) [data-email-summary-stat]>b{left:.203125px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(4) [data-email-summary-stat]{width:99.875px}[data-email-summary-metrics][data-summary-count="4"] [data-summary-column]:nth-child(4) [data-email-summary-stat]>b{left:.359375px}[data-email-summary-stat]{min-width:max-content}[data-email-summary-stat]>div{white-space:nowrap}}
`;

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  {{#if HasSubmittedEvents}}
  "target": "{{BaseUrl}}/project/{{ProjectId}}/error/timeline",
  "url": "{{BaseUrl}}/project/{{ProjectId}}/error/timeline",
  "name": "View Timeline"
  {{else}}
  "target": "{{BaseUrl}}/project/{{ProjectId}}/configure",
  "url": "{{BaseUrl}}/project/{{ProjectId}}/configure",
  "name": "Configure Project"
  {{/if}}
}
`);
</script>

<EmailLayout styles={dailySummaryStyles}>
    {#snippet content()}
        <Section data-email-content data-summary-blocked={'{{Blocked}}'} class="px-4 py-2">
            <Heading as="h1" class="text-foreground mt-0 mb-[5px] text-[34px] leading-[1.3] font-normal">{@html 'Summary for {{StartDate}}'}</Heading>

            {@html '{{#if HasSubmittedEvents}}{{#if Blocked}}'}
            <Section data-email-summary-metrics data-summary-count="4" class="my-4">
                <Row>
                    <Column data-summary-column width="25%" style="padding:4px;vertical-align:top">
                        <div data-email-summary-stat style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center">
                            <b>Count</b>
                            <div style="font-size:34px;font-weight:400;line-height:1.3;margin-top:3px;text-align:center">
                                {@html '{{Count}}'}
                            </div>
                        </div>
                    </Column>
                    <Column data-summary-column width="25%" style="padding:4px;vertical-align:top">
                        <div data-email-summary-stat style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center">
                            <b>Unique</b>
                            <div style="font-size:34px;font-weight:400;line-height:1.3;margin-top:3px;text-align:center">
                                {@html '{{Unique}}'}
                            </div>
                        </div>
                    </Column>
                    <Column data-summary-column width="25%" style="padding:4px;vertical-align:top">
                        <div data-email-summary-stat style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center">
                            <b>New</b>
                            <div style="font-size:34px;font-weight:400;line-height:1.3;margin-top:3px;text-align:center">
                                {@html '{{New}}'}
                            </div>
                        </div>
                    </Column>
                    <Column data-summary-column width="25%" style="padding:4px;vertical-align:top">
                        <div data-email-summary-stat style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center">
                            <b>Discarded</b>
                            <div style="font-size:34px;font-weight:400;line-height:1.3;margin-top:3px;text-align:center">
                                {@html '{{Blocked}}'}
                            </div>
                        </div>
                    </Column>
                </Row>
            </Section>
            {@html '{{else}}'}
            <Section data-email-summary-metrics data-summary-count="3" class="my-4">
                <Row>
                    <Column data-summary-column width="33%" style="padding:4px;vertical-align:top">
                        <div data-email-summary-stat style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center">
                            <b>Count</b>
                            <div style="font-size:34px;font-weight:400;line-height:1.3;margin-top:3px;text-align:center">
                                {@html '{{Count}}'}
                            </div>
                        </div>
                    </Column>
                    <Column data-summary-column width="33%" style="padding:4px;vertical-align:top">
                        <div data-email-summary-stat style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center">
                            <b>Unique</b>
                            <div style="font-size:34px;font-weight:400;line-height:1.3;margin-top:3px;text-align:center">
                                {@html '{{Unique}}'}
                            </div>
                        </div>
                    </Column>
                    <Column data-summary-column width="34%" style="padding:4px;vertical-align:top">
                        <div data-email-summary-stat style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center">
                            <b>New</b>
                            <div style="font-size:34px;font-weight:400;line-height:1.3;margin-top:3px;text-align:center">
                                {@html '{{New}}'}
                            </div>
                        </div>
                    </Column>
                </Row>
            </Section>
            {@html '{{/if}}{{/if}}'}

            {@html '{{#if HasSubmittedEvents}}'}
            <Text class="text-foreground text-base leading-[1.3]"
                >{@html '{{#if Count}}The "{{ProjectName}}" project had <strong>{{Count}} total</strong>, <strong>{{Unique}} unique</strong>, and <strong>{{New}} new</strong> errors.{{else}}Congrats! The "{{ProjectName}}" project was exceptionless!{{/if}}{{#if Fixed}} Additionally, <strong>{{Fixed}} errors</strong> that have been marked as fixed occurred in outdated instances of your application.{{/if}}'}</Text
            >

            <Section data-email-button-row data-email-timeline-button class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/project/{'{{ProjectId}}'}/error/timeline"
                    class="bg-primary inline-block rounded-[3px] px-4 py-2 text-base font-bold text-white no-underline">View Timeline</Button
                >
            </Section>

            {@html '{{#if Blocked}}'}
            <Section data-email-summary-alert class="border-destructive bg-destructive/10 my-4 rounded-[3px] border p-[10px]">
                <Text class="text-foreground text-base leading-[1.3]"
                    >{@html '<strong>{{Blocked}} events</strong> were discarded due to throttling. <a href="{{BaseUrl}}/organization/{{OrganizationId}}/upgrade" class="text-primary no-underline">Upgrade now</a> to increase your limits. <a href="https://github.com/exceptionless/Exceptionless/wiki/Frequently-Asked-Questions#q-why-is-my-organization-throttled" class="text-primary no-underline">Click here to learn more about throttling.</a>'}</Text
                >
                <Section data-email-button-row class="text-center">
                    <Button
                        href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/upgrade"
                        class="bg-destructive inline-block rounded-[3px] px-4 py-2 text-base font-bold text-white no-underline">Upgrade Plan</Button
                    >
                </Section>
            </Section>
            {@html '{{/if}}'}

            {@html '{{#if MostFrequent}}'}
            <Heading as="h5" class="text-foreground mt-0 mb-[5px] text-[20px] leading-[1.3] font-normal">Most Frequent</Heading>
            {@html '<ul style="margin-top:0">'}
            {@html '{{#each MostFrequent}}<li style="margin-top:5px;margin-left:5px"><a href="{{../BaseUrl}}/stack/{{StackId}}" class="text-primary no-underline">{{#if IsRegressed}}<strong>[REGRESSED]</strong> {{/if}}{{#if TypeName}}<strong>{{TypeName}}:</strong> {{/if}}{{Title}}</a></li>{{/each}}'}
            {@html '<li style="margin-top:5px;margin-left:5px"><a href="{{BaseUrl}}/project/{{ProjectId}}/error/frequent" class="text-primary no-underline">View more...</a></li></ul>'}
            {@html '{{/if}}'}

            {@html '{{#if Newest}}'}
            <Heading as="h5" class="text-foreground mt-0 mb-[5px] text-[20px] leading-[1.3] font-normal">Newest</Heading>
            {@html '<ul style="margin-top:0">'}
            {@html '{{#each Newest}}<li style="margin-top:5px;margin-left:5px"><a href="{{../BaseUrl}}/stack/{{StackId}}" class="text-primary no-underline">{{#if IsRegressed}}<strong>[REGRESSED]</strong> {{/if}}{{#if TypeName}}<strong>{{TypeName}}:</strong> {{/if}}{{Title}}</a></li>{{/each}}'}
            {@html '<li style="margin-top:5px;margin-left:5px"><a href="{{BaseUrl}}/project/{{ProjectId}}/error/new" class="text-primary no-underline">View more...</a></li></ul>'}
            {@html '{{/if}}'}

            {@html '{{#if IsFreePlan}}'}
            <Text data-email-free-plan class="text-foreground text-base leading-[1.3]"
                >You are currently on a free plan. If you would like to receive notifications for errors as they happen, <Link
                    href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/upgrade"
                    class="text-primary no-underline">upgrade to a paid plan</Link
                >.</Text
            >
            {@html '{{/if}}'}

            {@html '{{else}}'}
            <Text data-email-unconfigured class="text-foreground text-[20px] leading-[1.6]"
                >{@html 'Unfortunately, it appears that your "{{ProjectName}}" project has not yet been configured to send errors to'}
                <Link href="https://exceptionless.com" class="text-primary no-underline">Exceptionless</Link>.</Text
            >
            <Section data-email-button-row data-email-configure-button class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/project/{'{{ProjectId}}'}/configure"
                    class="bg-primary inline-block rounded-[3px] px-4 py-2 text-base font-bold text-white no-underline">Configure Project</Button
                >
            </Section>
            <Text class="text-foreground text-base leading-[1.3]"
                >Send us an email at <Link href="mailto:support@exceptionless.io" class="text-primary no-underline">support@exceptionless.io</Link> if you have any
                questions or need help getting started.</Text
            >
            {@html '{{/if}}'}
        </Section>

        <ActionsFooter summaryBlocked={'{{Blocked}}'}>
            {#snippet actions()}
                <li class="mt-[5px] ml-[5px]">
                    <Link href="{'{{BaseUrl}}'}/account/manage?projectId={'{{ProjectId}}'}&tab=notifications" class="text-primary no-underline"
                        >Change your notification settings for this project</Link
                    >
                </li>
            {/snippet}
        </ActionsFooter>
    {/snippet}
</EmailLayout>

{@html jsonLd}
