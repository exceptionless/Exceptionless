<script module lang="ts">
    import { Button, Text, Heading, Section } from '@better-svelte-email/components';
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
        <Section class="py-2 px-4">
            <Heading as="h1" class="text-[34px] font-normal text-dark leading-[1.3] mt-0 mb-[5px]"
                >Hello {'{{UserFullName}}'},</Heading
            >
            <Text class="text-[20px] leading-[1.6] text-dark"
                >We're ready to activate your account. All we need to do is make sure this is your email address.</Text
            >
            <Section class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/account/verify?token={'{{UserVerifyEmailAddressToken}}'}"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >Verify Address</Button
                >
            </Section>
            <Text class="text-base text-dark leading-[1.3]"
                >If you didn't create an Exceptionless account, just delete this email and everything will go back to
                the way it was.</Text
            >
        </Section>
    {/snippet}
</EmailLayout>

{@html jsonLd}
