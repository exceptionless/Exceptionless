<script module lang="ts">
    import { Button, Text, Heading, Section } from '@better-svelte-email/components';
    import EmailLayout from '../components/EmailLayout.svelte';
    import { buildEmailMetadata } from '../lib/email-metadata';

    const jsonLd = buildEmailMetadata(`
{
  "@type": "ViewAction",
  "target": "{{BaseUrl}}/reset-password/{{UserPasswordResetToken}}",
  "url": "{{BaseUrl}}/reset-password/{{UserPasswordResetToken}}",
  "name": "Reset Password"
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
                >We heard you need a password reset. Click the link below and you'll be redirected to a secure site from
                which you can set a new password.</Text
            >
            <Section class="text-center">
                <Button
                    href="{'{{BaseUrl}}'}/reset-password/{'{{UserPasswordResetToken}}'}"
                    class="bg-primary text-white font-bold text-base rounded-[3px] px-4 py-2 no-underline inline-block"
                    >Reset Password</Button
                >
            </Section>
            <Text class="text-base text-dark leading-[1.3]"
                >If you didn't try to reset your password, you can safely ignore this email. The reset link will expire
                automatically.</Text
            >
        </Section>
    {/snippet}
</EmailLayout>

{@html jsonLd}
