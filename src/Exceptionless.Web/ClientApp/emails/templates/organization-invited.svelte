<script module lang="ts">
    import { Button, Heading, Link, Section, Text } from '@better-svelte-email/components';

    import EmailLayout from '../components/EmailLayout.svelte';
    import SocialFooter, { socialFooterStyles } from '../components/SocialFooter.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  "target": "{{BaseUrl}}/signup?token={{InviteToken}}",
  "url": "{{BaseUrl}}/signup?token={{InviteToken}}",
  "name": "Join Organization"
}
`);
</script>

<EmailLayout styles={socialFooterStyles}>
    {#snippet content()}
        <Section data-email-content class="px-4 py-2">
            <Heading as="h1" class="text-foreground mt-0 mb-[5px] text-[34px] leading-[1.3] font-normal">You've been invited to become Exceptionless!</Heading>
            <Text class="text-foreground text-[20px] leading-[1.6]">{@html '{{Subject}}'}</Text>
            <Section data-email-button-row class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/signup?token={'{{InviteToken}}'}"
                    class="bg-primary inline-block rounded-[3px] px-4 py-2 text-base font-bold text-white no-underline">Join Organization</Button
                >
            </Section>
            <Text class="text-foreground text-base leading-[1.3]"
                >What is <Link href="https://exceptionless.com" class="text-primary no-underline">Exceptionless</Link>? Exceptionless is an error reporting
                service. Go from signing up to catching every error in your application in 15 minutes or less.</Text
            >
        </Section>
        <SocialFooter />
    {/snippet}
</EmailLayout>

{@html jsonLd}
