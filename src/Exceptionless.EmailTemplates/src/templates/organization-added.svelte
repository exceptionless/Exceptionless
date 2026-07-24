<script module lang="ts">
    import { Button, Text, Heading, Section, Link } from '@better-svelte-email/components';
    import EmailLayout from '../components/EmailLayout.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';
    import SocialFooter from '../components/SocialFooter.svelte';

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  "target": "{{BaseUrl}}/organization/{{OrganizationId}}/dashboard",
  "url": "{{BaseUrl}}/organization/{{OrganizationId}}/dashboard",
  "name": "View Organization"
}
`);
</script>

<EmailLayout>
    {#snippet content()}
        <Section class="py-2 px-4">
            <Heading as="h1" class="text-[34px] font-normal text-dark leading-[1.3] mt-0 mb-[5px]"
                >{@html '{{Subject}}'}</Heading
            >
            <Section class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/organization/{'{{OrganizationId}}'}/dashboard"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >View Organization</Button
                >
            </Section>
            <Text class="text-base text-dark leading-[1.3]"
                >What is <Link href="https://exceptionless.com" class="text-primary no-underline">Exceptionless</Link>?
                Exceptionless is an error reporting service. Go from signing up to catching every error in your
                application in 15 minutes or less.</Text
            >
        </Section>
        <SocialFooter />
    {/snippet}
</EmailLayout>

{@html jsonLd}
