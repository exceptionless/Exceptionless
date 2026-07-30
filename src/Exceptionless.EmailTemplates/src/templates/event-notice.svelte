<script module lang="ts">
    import { Button, Text, Heading, Section, Link } from '@better-svelte-email/components';
    import EmailLayout from '../components/EmailLayout.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';
    import ActionsFooter from '../components/ActionsFooter.svelte';

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  "target": "{{BaseUrl}}/event/{{EventId}}",
  "url": "{{BaseUrl}}/event/{{EventId}}",
  "name": "View Event Details"
}
`);
</script>

<EmailLayout>
    {#snippet content()}
        <Section data-email-content class="py-2 px-4">
            <Text class="text-[20px] leading-[1.6] text-dark"
                >{@html '{{#if IsNew}}A new {{#if IsCritical}}critical {{/if}}event has occurred in the "{{ProjectName}}" project.{{else if IsRegression}}{{#if IsCritical}}A critical{{else}}An{{/if}} event has regressed in the "{{ProjectName}}" project.{{else}}{{#if IsCritical}}A critical{{else}}An{{/if}} event has reoccurred for the {{TotalOccurrences}} time in the "{{ProjectName}}" project.{{/if}}'}</Text
            >

            <Section data-email-button-row class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/event/{'{{EventId}}'}"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >View Event Details</Button
                >
            </Section>

            {@html '{{#if Fields}}'}
            <Section data-email-fields-card class="border border-border rounded-[3px] bg-white p-[10px] my-4">
                {@html '{{#each Fields}}{{#if @index}}'}
                <hr style="border-color:#f7f7f7" />
                {@html '{{/if}}'}
                <Text class="text-base text-dark leading-[1.3] my-[10px]"
                    >{@html '<strong>{{@key}}</strong><br /><span style="word-wrap:break-word;word-break:break-all">{{this}}</span>'}</Text
                >
                {@html '{{/each}}'}
            </Section>
            {@html '{{/if}}'}

            {@html '{{#if HasUserInfo}}'}
            <Heading as="h4" class="text-[24px] font-normal text-dark leading-[1.3] mt-0 mb-[5px]">User Info</Heading>
            <Section data-email-user-card class="border border-border rounded-[3px] bg-white p-[10px] my-4">
                {@html '{{#if UserDisplayName}}'}
                <Text class="text-base text-dark leading-[1.3] my-[10px]"
                    >{@html '<strong>Name</strong><br />{{#if UserEmail}}<a href="{{UserEmailHref}}" style="color:#5E9A00;text-decoration:none">{{UserDisplayName}}</a>{{else}}<span style="word-wrap:break-word;word-break:break-all">{{UserDisplayName}}</span>{{/if}}'}</Text
                >
                {@html '{{#if UserDescription}}'}
                <hr style="border-color:#f7f7f7" />
                {@html '{{/if}}{{/if}}'}
                {@html '{{#if UserDescription}}'}
                <Text class="text-base text-dark leading-[1.3] my-[10px]"
                    >{@html '<strong>Description</strong><br /><span style="word-wrap:break-word;word-break:break-all">{{UserDescription}}</span>'}</Text
                >
                {@html '{{/if}}'}
            </Section>
            {@html '{{/if}}'}
        </Section>

        <ActionsFooter>
            {#snippet actions()}
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/project/{'{{ProjectId}}'}/stacks/{'{{StackId}}'}"
                        class="text-primary-action no-underline">Mark event as fixed</Link
                    >
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/project/{'{{ProjectId}}'}/stacks/{'{{StackId}}'}"
                        class="text-primary-action no-underline">Stop sending notifications for this event</Link
                    >
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/project/{'{{ProjectId}}'}/stacks/{'{{StackId}}'}"
                        class="text-primary-action no-underline">Discard future event occurrences</Link
                    >
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/account/notifications?project={'{{ProjectId}}'}"
                        class="text-primary-action no-underline"
                        >Change your notification settings for this project</Link
                    >
                </li>
            {/snippet}
        </ActionsFooter>
    {/snippet}
</EmailLayout>

{@html jsonLd}
