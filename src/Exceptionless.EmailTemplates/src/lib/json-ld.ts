/**
 * Wraps JSON-LD content in a `<script type="application/ld+json">` block.
 *
 * This lives in a plain .ts module rather than inline inside Svelte `<script module>`
 * blocks because the Svelte HTML parser scans for the literal string `</script>` to
 * close the enclosing script element. Having `</script>` (or `<script`) anywhere inside
 * a template literal in a Svelte script block causes a parse error.
 */
export function wrapJsonLd(content: string): string {
    return '<script type="application/ld+json">\n' + content.trim() + '\n</script>';
}
