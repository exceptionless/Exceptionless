<script module lang="ts">
    import { Button, Text, Heading, Section, Link } from '@better-svelte-email/components';
    import EmailLayout from '../components/EmailLayout.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';
    import ActionsFooter from '../components/ActionsFooter.svelte';

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  "target": "{{BaseUrl}}/organization/{{OrganizationId}}/manage?tab=billing",
  "url": "{{BaseUrl}}/organization/{{OrganizationId}}/manage?tab=billing",
  "name": "Update Billing Information"
}
`);
</script>

<EmailLayout>
    {#snippet content()}
        <Section class="py-2 px-4">
            <Heading as="h1" class="text-[34px] font-normal text-dark leading-[1.3] mt-0 mb-[5px]"
                >Payment Failed</Heading
            >
            <Text class="text-[20px] leading-[1.6] text-dark"
                >{@html 'Payment failed for organization "{{OrganizationName}}". In order to avoid service interruption, please login and update your payment information.'}</Text
            >
            <Section class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/manage?tab=billing"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >Update Billing Information</Button
                >
            </Section>
            <Text class="text-base text-dark leading-[1.3]"
                >Send us an email at <Link href="mailto:support@exceptionless.io" class="text-primary no-underline"
                    >support@exceptionless.io</Link
                > if you have any questions or need assistance.</Text
            >
        </Section>
        <ActionsFooter>
            {#snippet actions()}
                <li class="mt-[5px] ml-[5px]">
                    <Link
                        href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/manage?tab=billing"
                        class="text-primary-action no-underline">View invoices</Link
                    >
                </li>
            {/snippet}
        </ActionsFooter>
    {/snippet}
</EmailLayout>

{@html jsonLd}
