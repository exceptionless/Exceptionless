<script module lang="ts">
    import { Button, Heading, Link, Section, Text } from '@better-svelte-email/components';

    import ActionsFooter, { actionsFooterStyles } from '../components/ActionsFooter.svelte';
    import EmailLayout from '../components/EmailLayout.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  "target": "{{BaseUrl}}/organization/{{OrganizationId}}/manage?tab=billing",
  "url": "{{BaseUrl}}/organization/{{OrganizationId}}/manage?tab=billing",
  "name": "Update Billing Information"
}
`);
</script>

<EmailLayout styles={actionsFooterStyles}>
    {#snippet content()}
        <Section data-email-content class="px-4 py-2">
            <Heading as="h1" class="text-dark mt-0 mb-[5px] text-[34px] leading-[1.3] font-normal">Payment Failed</Heading>
            <Text class="text-dark text-[20px] leading-[1.6]"
                >{@html 'Payment failed for organization "{{OrganizationName}}". In order to avoid service interruption, please login and update your payment information.'}</Text
            >
            <Section data-email-button-row class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/manage?tab=billing"
                    class="bg-primary inline-block rounded-[3px] px-4 py-2 text-base font-bold text-white no-underline">Update Billing Information</Button
                >
            </Section>
            <Text class="text-dark text-base leading-[1.3]"
                >Send us an email at <Link href="mailto:support@exceptionless.io" class="text-primary no-underline">support@exceptionless.io</Link> if you have any
                questions or need assistance.</Text
            >
        </Section>
        <ActionsFooter>
            {#snippet actions()}
                <li class="mt-[5px] ml-[5px]">
                    <Link href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/manage?tab=billing" class="text-primary-action no-underline"
                        >View invoices</Link
                    >
                </li>
            {/snippet}
        </ActionsFooter>
    {/snippet}
</EmailLayout>

{@html jsonLd}
