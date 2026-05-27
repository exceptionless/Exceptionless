<script module>
  // @ts-nocheck
  import { Section, Heading, Text, Link } from '@better-svelte-email/components';
</script>

<Section class="bg-[#f7f7f7] py-4 px-4">
  {@html `<table width="100%" cellpadding="0" cellspacing="0" role="presentation"><tbody><tr>
    <td width="50%" style="vertical-align:top;padding:8px">`}
      <Heading as="h5" class="text-[20px] font-normal text-[#2c2c2c] leading-[1.3] mt-0 mb-[5px]">Connect With Us:</Heading>
      {@html `<table width="100%" cellpadding="0" cellspacing="4" role="presentation"><tbody>
        <tr><td><a href="https://www.facebook.com/exceptionless/" style="display:block;background:#3B5998;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px">Facebook</a></td></tr>
        <tr><td><a href="https://twitter.com/exceptionless" style="display:block;background:#1daced;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px">Twitter</a></td></tr>
        <tr><td><a href="https://github.com/exceptionless/exceptionless" style="display:block;background:#444444;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px">GitHub</a></td></tr>
      </tbody></table>`}
  {@html `</td>
    <td width="50%" style="vertical-align:top;padding:8px">`}
      <Heading as="h5" class="text-[20px] font-normal text-[#2c2c2c] leading-[1.3] mt-0 mb-[5px]">Contact Info:</Heading>
      <Text class="text-base text-[#2c2c2c] leading-[1.3]">Email: <Link href="mailto:support@exceptionless.io" class="text-[#5E9A00] no-underline">support@exceptionless.io</Link></Text>
  {@html `</td>
  </tr></tbody></table>`}
</Section>
