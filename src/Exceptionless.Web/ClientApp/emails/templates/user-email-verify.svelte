<script module lang="ts">
    import { Button, Heading, Section, Text } from '@better-svelte-email/components';

    import EmailLayout from '../components/EmailLayout.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  "target": "{{BaseUrl}}/account/verify?token={{UserVerifyEmailAddressToken}}",
  "url": "{{BaseUrl}}/account/verify?token={{UserVerifyEmailAddressToken}}",
  "name": "Verify Address"
}
`);
</script>

<EmailLayout>
    {#snippet content()}
        <Section data-email-content class="px-4 py-2">
            <Heading as="h1" class="text-foreground mt-0 mb-[5px] text-[34px] leading-[1.3] font-normal">Hello {'{{UserFullName}}'},</Heading>
            <Text class="text-foreground text-[20px] leading-[1.6]"
                >We're ready to activate your account. All we need to do is make sure this is your email address.</Text
            >
            <Section data-email-button-row class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/account/verify?token={'{{UserVerifyEmailAddressToken}}'}"
                    class="bg-primary inline-block rounded-[3px] px-4 py-2 text-base font-bold text-white no-underline">Verify Address</Button
                >
            </Section>
            <Text class="text-foreground text-base leading-[1.3]"
                >If you didn't create an Exceptionless account, just delete this email and everything will go back to the way it was.</Text
            >
        </Section>
    {/snippet}
</EmailLayout>

{@html jsonLd}
