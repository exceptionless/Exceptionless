<script module>
  // @ts-nocheck
  import { Button, Text, Heading, Section, Link } from '@better-svelte-email/components';
  import EmailLayout from '../components/EmailLayout.svelte';
  import SocialFooter from '../components/SocialFooter.svelte';
</script>

<EmailLayout>
  {#snippet content()}
    <Section class="py-2 px-4">
      <Heading as="h1" class="text-[34px] font-normal text-[#2c2c2c] leading-[1.3] mt-0 mb-[5px]">You've been invited to become Exceptionless!</Heading>
      <Text class="text-[20px] leading-[1.6] text-[#2c2c2c]">{@html '{{Subject}}'}</Text>
      <Section class="text-center">
        <Button href="{'{{BaseUrl}}'}/signup?token={'{{InviteToken}}'}" class="bg-[#5E9A00] text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block">Join Organization</Button>
      </Section>
      <Text class="text-base text-[#2c2c2c] leading-[1.3]">What is <Link href="https://exceptionless.com" class="text-[#5E9A00] no-underline">Exceptionless</Link>? Exceptionless is an error reporting service. Go from signing up to catching every error in your application in 15 minutes or less.</Text>
    </Section>
    <SocialFooter />
  {/snippet}
</EmailLayout>

{@html `<script type="application/ld+json">
{
  "@context": "http://schema.org",
  "@type": "EmailMessage",
  "description": "{{Subject}}",
  "potentialAction": {
    "@type": "ViewAction",
    "target": "{{BaseUrl}}/signup?token={{InviteToken}}",
    "url": "{{BaseUrl}}/signup?token={{InviteToken}}",
    "name": "Join Organization"
  },
  "publisher": {
    "@type": "Organization",
    "name": "Exceptionless",
    "url": "https://exceptionless.com",
    "logo": "https://be.exceptionless.io/img/exceptionless-48.png"
  }
}
</script>`}
