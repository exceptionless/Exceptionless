<script module lang="ts">
    import { Section } from '@better-svelte-email/components';
</script>

<Section class="bg-bg py-4 px-4">
    <!-- eslint-disable-next-line svelte/no-at-html-tags -- intentional: two-column table layout required for email client compatibility -->
    {@html `<table width="100%" cellpadding="0" cellspacing="0" role="presentation"><tbody><tr>
    <td width="50%" style="vertical-align:top;padding:8px">
      <h5 style="font-size:20px;font-weight:400;color:#2c2c2c;line-height:1.3;margin-top:0;margin-bottom:5px">Connect With Us:</h5>
      <table width="100%" cellpadding="0" cellspacing="4" role="presentation"><tbody>
        <tr><td><a href="https://www.facebook.com/exceptionless/" style="display:block;background:#3B5998;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px">Facebook</a></td></tr>
        <tr><td><a href="https://twitter.com/exceptionless" style="display:block;background:#1daced;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px">Twitter</a></td></tr>
        <tr><td><a href="https://github.com/exceptionless/exceptionless" style="display:block;background:#444444;color:#fefefe;font-weight:700;font-size:16px;text-align:center;text-decoration:none;padding:8px 16px;border-radius:3px">GitHub</a></td></tr>
      </tbody></table>
    </td>
    <td width="50%" style="vertical-align:top;padding:8px">
      <h5 style="font-size:20px;font-weight:400;color:#2c2c2c;line-height:1.3;margin-top:0;margin-bottom:5px">Contact Info:</h5>
      <p style="font-size:16px;color:#2c2c2c;line-height:1.3;margin:0 0 10px">Email: <a href="mailto:support@exceptionless.io" style="color:#5E9A00;text-decoration:none">support@exceptionless.io</a></p>
    </td>
  </tr></tbody></table>`}
</Section>
