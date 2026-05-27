<script module lang="ts">
    import { Button, Text, Heading, Section, Hr, Link } from '@better-svelte-email/components';
    import EmailLayout from '../components/EmailLayout.svelte';
    import ActionsFooter from '../components/ActionsFooter.svelte';
</script>

<EmailLayout>
    {#snippet content()}
        <Section class="py-2 px-4">
            <Text class="text-[20px] leading-[1.6] text-dark"
                >{@html '{{#if IsNew}}A new {{#if IsCritical}}critical {{/if}}event has occurred in the "{{ProjectName}}" project.{{else if IsRegression}}{{#if IsCritical}}A critical{{else}}An{{/if}} event has regressed in the "{{ProjectName}}" project.{{else}}{{#if IsCritical}}A critical{{else}}An{{/if}} event has reoccurred for the {{TotalOccurrences}} time in the "{{ProjectName}}" project.{{/if}}'}</Text
            >

            <Section class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/event/{'{{EventId}}'}"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >View Event Details</Button
                >
            </Section>

            {@html '{{#if Fields}}'}
            <Section class="border border-border rounded-[3px] bg-white p-[10px] my-4">
                {@html '{{#each Fields}}{{#if @index}}'}
                <Hr class="border-bg" />
                {@html '{{/if}}'}
                <Text class="text-base text-dark leading-[1.3] my-[10px]"
                    >{@html '<strong>{{@key}}</strong><br /><span style="word-wrap:break-word;word-break:break-all">{{this}}</span>'}</Text
                >
                {@html '{{/each}}'}
            </Section>
            {@html '{{/if}}'}

            {@html '{{#if HasUserInfo}}'}
            <Heading as="h4" class="text-[24px] font-normal text-dark leading-[1.3] mt-0 mb-[5px]">User Info</Heading>
            <Section class="border border-border rounded-[3px] bg-white p-[10px] my-4">
                {@html '{{#if UserDisplayName}}'}
                <Text class="text-base text-dark leading-[1.3] my-[10px]"
                    >{@html '<strong>Name</strong><br />{{#if UserEmail}}<a href="mailto:{{UserEmail}}?body={{UserDescription}}" style="color:#5E9A00;text-decoration:none">{{UserDisplayName}}</a>{{else}}<span style="word-wrap:break-word;word-break:break-all">{{UserDisplayName}}</span>{{/if}}'}</Text
                >
                {@html '{{#if UserDescription}}'}
                <Hr class="border-bg" />
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
                        href="{'{{BaseUrl}}'}/stack/{'{{StackId}}'}/mark-fixed"
                        class="text-primary-action no-underline">Mark event as fixed</Link
                    >
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link href="{'{{BaseUrl}}'}/stack/{'{{StackId}}'}/ignored" class="text-primary-action no-underline"
                        >Stop sending notifications for this event</Link
                    >
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/stack/{'{{StackId}}'}/discarded"
                        class="text-primary-action no-underline">Discard future event occurrences</Link
                    >
                </li>
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
    "target": "{{BaseUrl}}/event/{{EventId}}",
    "url": "{{BaseUrl}}/event/{{EventId}}",
    "name": "View Event Details"
  },
  "publisher": {
    "@type": "Organization",
    "name": "Exceptionless",
    "url": "https://exceptionless.com",
    "logo": "https://be.exceptionless.io/img/exceptionless-48.png"
  }
}
</script>`}
