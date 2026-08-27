<script module>
  // @ts-nocheck
  import { Html, Head, Body, Container, Section, Row, Column, Button, Text, Heading, Img, Hr, Link } from '@better-svelte-email/components';
</script>

<script>
  let { content = '', preheader = '' } = $props();
</script>

<Html lang="en">
  <Head />
  <Body class="bg-[#f7f7f7] font-[Helvetica,Arial,sans-serif]">
    {#if preheader}
      <span class="hidden max-h-0 max-w-0 overflow-hidden opacity-0" style="display:none!important;font-size:1px;line-height:1px;max-height:0;max-width:0;mso-hide:all!important;opacity:0;overflow:hidden;visibility:hidden">{@html preheader}</span>
    {/if}
    <Section class="bg-[#2c2c2c] w-full py-2 px-4">
      <Container class="max-w-[580px] mx-auto">
        <Img src="https://be.exceptionless.io/img/exceptionless-logo.png" alt="Exceptionless" class="block ml-4" />
      </Container>
    </Section>
    <Container class="max-w-[580px] mx-auto bg-[#f7f7f7]">
      {@render content()}
    </Container>
    <!-- prevent Gmail on iOS font size manipulation -->
    <div style="display:none;white-space:nowrap;font:15px courier;line-height:0">&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;</div>
  </Body>
</Html>
