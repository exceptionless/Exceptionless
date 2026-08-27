export function wrapJsonLd(content: string): string {
    return '<script type="application/ld+json">\n' + content.trim() + '\n</script>';
}
