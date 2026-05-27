<script lang="ts">
    /**
     * Renders a pre-built email template HTML in an iframe.
     * The HTML is the actual compiled output from the build pipeline,
     * with Handlebars tokens pre-filled via fillTokens().
     */
    let { html = '', height = 800 }: { html: string; height?: number } = $props();
</script>

<div style="padding: 16px; background: #e5e7eb; min-height: 100vh;">
    <iframe
        srcdoc={html}
        style="width: 100%; height: {height}px; border: 1px solid #d1d5db; border-radius: 4px; background: white;"
        title="Email Preview"
    ></iframe>
</div>
