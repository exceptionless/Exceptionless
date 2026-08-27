<script module>
  // @ts-nocheck
  import { Section, Heading } from '@better-svelte-email/components';
</script>

<script>
  let { actions } = $props();
</script>

<Section class="px-4 py-2">
  <Heading as="h5" class="text-[20px] font-normal text-[#939393] leading-[1.3] mt-0 mb-[5px]">Other Actions</Heading>
  <ul style="margin-top:0">
    {@render actions()}
  </ul>
</Section>
