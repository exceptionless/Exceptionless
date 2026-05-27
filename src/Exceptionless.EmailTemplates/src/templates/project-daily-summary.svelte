<script module lang="ts">
    import { Button, Text, Heading, Section, Link } from '@better-svelte-email/components';
    import EmailLayout from '../components/EmailLayout.svelte';
    import ActionsFooter from '../components/ActionsFooter.svelte';
</script>

<EmailLayout>
    {#snippet content()}
        <Section class="py-2 px-4">
            <Heading as="h1" class="text-[34px] font-normal text-dark leading-[1.3] mt-0 mb-[5px]"
                >{@html 'Summary for {{StartDate}}'}</Heading
            >

            {@html '{{#if HasSubmittedEvents}}{{#if Blocked}}'}
            <Section class="my-4">
                {@html '<table width="100%" cellpadding="0" cellspacing="0" role="presentation"><tbody><tr><td width="25%" style="padding:4px;vertical-align:top"><div style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center"><b>Count</b><div style="font-size:34px;font-weight:400;text-align:center">{{Count}}</div></div></td><td width="25%" style="padding:4px;vertical-align:top"><div style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center"><b>Unique</b><div style="font-size:34px;font-weight:400;text-align:center">{{Unique}}</div></div></td><td width="25%" style="padding:4px;vertical-align:top"><div style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center"><b>New</b><div style="font-size:34px;font-weight:400;text-align:center">{{New}}</div></div></td><td width="25%" style="padding:4px;vertical-align:top"><div style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center"><b>Discarded</b><div style="font-size:34px;font-weight:400;text-align:center">{{Blocked}}</div></div></td></tr></tbody></table>'}
            </Section>
            {@html '{{else}}'}
            <Section class="my-4">
                {@html '<table width="100%" cellpadding="0" cellspacing="0" role="presentation"><tbody><tr><td width="33%" style="padding:4px;vertical-align:top"><div style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center"><b>Count</b><div style="font-size:34px;font-weight:400;text-align:center">{{Count}}</div></div></td><td width="33%" style="padding:4px;vertical-align:top"><div style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center"><b>Unique</b><div style="font-size:34px;font-weight:400;text-align:center">{{Unique}}</div></div></td><td width="34%" style="padding:4px;vertical-align:top"><div style="background:#fefefe;border:1px solid #cbcbcb;padding:10px;text-align:center"><b>New</b><div style="font-size:34px;font-weight:400;text-align:center">{{New}}</div></div></td></tr></tbody></table>'}
            </Section>
            {@html '{{/if}}{{/if}}'}

            {@html '{{#if HasSubmittedEvents}}'}
            <Text class="text-base text-dark leading-[1.3]"
                >{@html '{{#if Count}}The "{{ProjectName}}" project had <strong>{{Count}} total</strong>, <strong>{{Unique}} unique</strong>, and <strong>{{New}} new</strong> errors.{{else}}Congrats! The "{{ProjectName}}" project was exceptionless!{{/if}}{{#if Fixed}} Additionally, <strong>{{Fixed}} errors</strong> that have been marked as fixed occurred in outdated instances of your application.{{/if}}'}</Text
            >

            <Section class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/project/{'{{ProjectId}}'}/error/timeline"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >View Timeline</Button
                >
            </Section>

            {@html '{{#if Blocked}}'}
            <Section class="border border-alert bg-alert-bg p-[10px] my-4 rounded-[3px]">
                <Text class="text-base text-dark leading-[1.3]"
                    >{@html '<strong>{{Blocked}} events</strong> were discarded due to throttling.'}
                    <Link
                        href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/upgrade"
                        class="text-primary no-underline">Upgrade now</Link
                    > to increase your limits. <Link
                        href="https://github.com/exceptionless/Exceptionless/wiki/Frequently-Asked-Questions#q-why-is-my-organization-throttled"
                        class="text-primary no-underline">Click here to learn more about throttling.</Link
                    ></Text
                >
                <Section class="text-center">
                    <Button
                        href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/upgrade"
                        class="bg-alert text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                        >Upgrade Plan</Button
                    >
                </Section>
            </Section>
            {@html '{{/if}}'}

            {@html '{{#if MostFrequent}}'}
            <Heading as="h5" class="text-[20px] font-normal text-muted leading-[1.3] mt-0 mb-[5px]"
                >Most Frequent</Heading
            >
            {@html '{{#each MostFrequent}}'}
            {@html '<ul style="margin-top:0"><li style="margin-top:5px;margin-left:5px"><a href="{{../BaseUrl}}/stack/{{StackId}}" style="color:#5E9A00;text-decoration:none">{{#if IsRegressed}}<strong>[REGRESSED]</strong> {{/if}}{{#if TypeName}}<strong>{{TypeName}}:</strong> {{/if}}{{Title}}</a></li></ul>'}
            {@html '{{/each}}'}
            {@html '<ul style="margin-top:0"><li style="margin-top:5px;margin-left:5px"><a href="{{BaseUrl}}/project/{{ProjectId}}/error/frequent" style="color:#5E9A00;text-decoration:none">View more...</a></li></ul>'}
            {@html '{{/if}}'}

            {@html '{{#if Newest}}'}
            <Heading as="h5" class="text-[20px] font-normal text-muted leading-[1.3] mt-0 mb-[5px]">Newest</Heading>
            {@html '{{#each Newest}}'}
            {@html '<ul style="margin-top:0"><li style="margin-top:5px;margin-left:5px"><a href="{{../BaseUrl}}/stack/{{StackId}}" style="color:#5E9A00;text-decoration:none">{{#if IsRegressed}}<strong>[REGRESSED]</strong> {{/if}}{{#if TypeName}}<strong>{{TypeName}}:</strong> {{/if}}{{Title}}</a></li></ul>'}
            {@html '{{/each}}'}
            {@html '<ul style="margin-top:0"><li style="margin-top:5px;margin-left:5px"><a href="{{BaseUrl}}/project/{{ProjectId}}/error/new" style="color:#5E9A00;text-decoration:none">View more...</a></li></ul>'}
            {@html '{{/if}}'}

            {@html '{{#if IsFreePlan}}'}
            <Text class="text-base text-dark leading-[1.3]"
                >You are currently on a free plan. If you would like to receive notifications for errors as they happen, <Link
                    href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/upgrade"
                    class="text-primary no-underline">upgrade to a paid plan</Link
                >.</Text
            >
            {@html '{{/if}}'}

            {@html '{{else}}'}
            <Text class="text-[20px] leading-[1.6] text-dark"
                >{@html 'Unfortunately, it appears that your "{{ProjectName}}" project has not yet been configured to send errors to'}
                <Link href="https://exceptionless.com" class="text-primary no-underline">Exceptionless</Link>.</Text
            >
            <Section class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/project/{'{{ProjectId}}'}/configure"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >Configure Project</Button
                >
            </Section>
            <Text class="text-base text-dark leading-[1.3]"
                >Send us an email at <Link href="mailto:support@exceptionless.io" class="text-primary no-underline"
                    >support@exceptionless.io</Link
                > if you have any questions or need help getting started.</Text
            >
            {@html '{{/if}}'}
        </Section>

        <ActionsFooter>
            {#snippet actions()}
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/account/manage?projectId={'{{ProjectId}}'}&tab=notifications"
                        class="text-primary-action no-underline"
                        >Change your notification settings for this project</Link
                    >
                </li>
            {/snippet}
        </ActionsFooter>
    {/snippet}
</EmailLayout>

{@html `<script type="application/ld+json">
{
  "@context": "http://schema.org",
  "@type": "EmailMessage",
  "description": "{{Subject}}",
  "potentialAction": {
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
  },
  "publisher": {
    "@type": "Organization",
    "name": "Exceptionless",
    "url": "https://exceptionless.com",
    "logo": "https://be.exceptionless.io/img/exceptionless-48.png"
  }
}
</script>`}
