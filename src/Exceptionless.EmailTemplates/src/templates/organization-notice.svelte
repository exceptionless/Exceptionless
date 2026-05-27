<script module lang="ts">
    import { Button, Text, Section, Link } from '@better-svelte-email/components';
    import EmailLayout from '../components/EmailLayout.svelte';
    import ActionsFooter from '../components/ActionsFooter.svelte';
</script>

<EmailLayout>
    {#snippet content()}
        <Section class="py-2 px-4">
            <Text class="text-[20px] leading-[1.6] text-dark"
                >{@html '{{#if IsOverMonthlyLimit}}{{OrganizationName}} has reached its monthly plan limit. Upgrade now to to continue receiving events.{{else if IsOverHourlyLimit}}Events are currently being throttled for {{OrganizationName}} until {{ThrottledUntil}} UTC to prevent using up your plan limit in a small window of time. Upgrade now to increase your limits.{{/if}}'}</Text
            >
            <Section class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/upgrade"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >Upgrade Plan</Button
                >
            </Section>
            <Text class="text-base text-dark leading-[1.3]"
                >{@html '{{#if IsOverMonthlyLimit}}'}<Link
                    href="https://github.com/exceptionless/Exceptionless/wiki/Frequently-Asked-Questions#q-what-happens-if-the-organization-plan-limit-is-reached"
                    class="text-primary no-underline"
                    >Learn more about what happens when the plan limit is reached.</Link
                >{@html '{{else if IsOverHourlyLimit}}'}<Link
                    href="https://github.com/exceptionless/Exceptionless/wiki/Frequently-Asked-Questions#q-why-is-my-organization-throttled"
                    class="text-primary no-underline">Learn more about being throttled.</Link
                >{@html '{{/if}}'} You can also view the <Link
                    href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/frequent"
                    class="text-primary no-underline">most frequent events</Link
                > to to see an overall picture of the events that are being counting against your plan limits.</Text
            >
            <Text class="text-base text-dark leading-[1.3]"
                >Please send us an email at <Link
                    href="mailto:support@exceptionless.io"
                    class="text-primary no-underline">support@exceptionless.io</Link
                > if you have any questions or concerns.</Text
            >
        </Section>
        <ActionsFooter>
            {#snippet actions()}
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/manage"
                        class="text-primary-action no-underline">View usage</Link
                    >
                </li>
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/account/manage?tab=notifications"
                        class="text-primary-action no-underline">Change your notification settings</Link
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
    "target": "{{BaseUrl}}/organization/{{OrganizationId}}/upgrade",
    "url": "{{BaseUrl}}/organization/{{OrganizationId}}/upgrade",
    "name": "Upgrade Plan"
  },
  "publisher": {
    "@type": "Organization",
    "name": "Exceptionless",
    "url": "https://exceptionless.com",
    "logo": "https://be.exceptionless.io/img/exceptionless-48.png"
  }
}
</script>`}
