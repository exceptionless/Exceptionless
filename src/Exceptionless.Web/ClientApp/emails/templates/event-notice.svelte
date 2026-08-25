<script module lang="ts">
    import { Button, Heading, Link, Section, Text } from '@better-svelte-email/components';

    import ActionsFooter, { actionsFooterStyles } from '../components/ActionsFooter.svelte';
    import EmailLayout from '../components/EmailLayout.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';

    const eventStyles = `${actionsFooterStyles}
[data-email-fields-card]{margin-bottom:21px!important}
[data-email-user-card]{margin-top:5px!important}
[data-email-fields-card]>tbody>tr>td,[data-email-user-card]>tbody>tr>td{padding:10px}
`;

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  "target": "{{BaseUrl}}/event/{{EventId}}",
  "url": "{{BaseUrl}}/event/{{EventId}}",
  "name": "View Event Details"
}
`);
</script>

<EmailLayout styles={eventStyles}>
    {#snippet content()}
        <Section data-email-content class="px-4 py-2">
            <Text class="text-dark text-[20px] leading-[1.6]"
                >{@html '{{#if IsNew}}A new {{#if IsCritical}}critical {{/if}}event has occurred in the "{{ProjectName}}" project.{{else if IsRegression}}{{#if IsCritical}}A critical{{else}}An{{/if}} event has regressed in the "{{ProjectName}}" project.{{else}}{{#if IsCritical}}A critical{{else}}An{{/if}} event has reoccurred for the {{TotalOccurrences}} time in the "{{ProjectName}}" project.{{/if}}'}</Text
            >

            <Section data-email-button-row class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/event/{'{{EventId}}'}"
                    class="bg-primary inline-block rounded-[3px] px-4 py-2 text-base font-bold text-white no-underline">View Event Details</Button
                >
            </Section>

            {@html '{{#if Fields}}'}
            <Section data-email-fields-card class="border-border my-4 rounded-[3px] border bg-white p-[10px]">
                {@html '{{#each Fields}}{{#if @index}}'}
                <hr style="border-color:#f7f7f7" />
                {@html '{{/if}}'}
                <Text class="text-dark my-[10px] text-base leading-[1.3]"
                    >{@html '<strong>{{@key}}</strong><br /><span style="word-wrap:break-word;word-break:break-all">{{this}}</span>'}</Text
                >
                {@html '{{/each}}'}
            </Section>
            {@html '{{/if}}'}

            {@html '{{#if HasUserInfo}}'}
            <Heading as="h4" class="text-dark mt-0 mb-[5px] text-[24px] leading-[1.3] font-normal">User Info</Heading>
            <Section data-email-user-card class="border-border my-4 rounded-[3px] border bg-white p-[10px]">
                {@html '{{#if UserDisplayName}}'}
                <Text class="text-dark my-[10px] text-base leading-[1.3]"
                    >{@html '<strong>Name</strong><br />{{#if UserEmail}}<a href="{{UserEmailHref}}" style="color:#5E9A00;text-decoration:none">{{UserDisplayName}}</a>{{else}}<span style="word-wrap:break-word;word-break:break-all">{{UserDisplayName}}</span>{{/if}}'}</Text
                >
                {@html '{{#if UserDescription}}'}
                <hr style="border-color:#f7f7f7" />
                {@html '{{/if}}{{/if}}'}
                {@html '{{#if UserDescription}}'}
                <Text class="text-dark my-[10px] text-base leading-[1.3]"
                    >{@html '<strong>Description</strong><br /><span style="word-wrap:break-word;word-break:break-all">{{UserDescription}}</span>'}</Text
                >
                {@html '{{/if}}'}
            </Section>
            {@html '{{/if}}'}
        </Section>

        <ActionsFooter>
            {#snippet actions()}
                <li class="mt-[5px] ml-[5px]">
                    <Link href="{'{{BaseUrl}}'}/stack/{'{{StackId}}'}/mark-fixed" class="text-primary-action no-underline">Mark event as fixed</Link>
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link href="{'{{BaseUrl}}'}/stack/{'{{StackId}}'}/ignored" class="text-primary-action no-underline"
                        >Stop sending notifications for this event</Link
                    >
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link href="{'{{BaseUrl}}'}/stack/{'{{StackId}}'}/discarded" class="text-primary-action no-underline">Discard future event occurrences</Link
                    >
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link href="{'{{BaseUrl}}'}/account/manage?projectId={'{{ProjectId}}'}&tab=notifications" class="text-primary-action no-underline"
                        >Change your notification settings for this project</Link
                    >
                </li>
            {/snippet}
        </ActionsFooter>
    {/snippet}
</EmailLayout>

{@html jsonLd}
