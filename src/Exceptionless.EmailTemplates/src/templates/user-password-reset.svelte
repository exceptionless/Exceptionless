<script module>
  // @ts-nocheck
  import { Button, Text, Heading, Section, Hr, Link } from '@better-svelte-email/components';
  import EmailLayout from '../components/EmailLayout.svelte';
</script>

<EmailLayout>
  {#snippet content()}
    <Section class="py-2 px-4">
      <Heading as="h1" class="text-[34px] font-normal text-[#2c2c2c] leading-[1.3] mt-0 mb-[5px]">Hello {'{{UserFullName}}'},</Heading>
      <Text class="text-[20px] leading-[1.6] text-[#2c2c2c]">We heard you need a password reset. Click the link below and you'll be redirected to a secure site from which you can set a new password.</Text>
      <Section class="text-center">
        <Button href="{'{{BaseUrl}}'}/reset-password/{'{{UserPasswordResetToken}}'}" class="bg-[#5E9A00] text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block">Reset Password</Button>
      </Section>
      <Text class="text-base text-[#2c2c2c] leading-[1.3]">If you didn't try to reset your password, <Link href="{'{{BaseUrl}}'}/reset-password/{'{{UserPasswordResetToken}}'}?cancel=true" class="text-[#5E9A00] no-underline">click here to cancel the password reset request</Link> and we'll forget this ever happened.</Text>
    </Section>
  {/snippet}
</EmailLayout>

{@html `<script type="application/ld+json">
{
  "@context": "http://schema.org",
  "@type": "EmailMessage",
  "description": "{{Subject}}",
  "potentialAction": {
    "@type": "ViewAction",
    "target": "{{BaseUrl}}/reset-password/{{UserPasswordResetToken}}",
    "url": "{{BaseUrl}}/reset-password/{{UserPasswordResetToken}}",
    "name": "Reset Password"
  },
  "publisher": {
    "@type": "Organization",
    "name": "Exceptionless",
    "url": "https://exceptionless.com",
    "logo": "https://be.exceptionless.io/img/exceptionless-48.png"
  }
}
</script>`}
