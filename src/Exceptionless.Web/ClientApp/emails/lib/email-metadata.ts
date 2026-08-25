import { wrapJsonLd } from './json-ld';

export function buildEmailMetadata(potentialAction: string): string {
    return wrapJsonLd(`
{
  "@context": "http://schema.org",
  "@type": "EmailMessage",
  "description": "{{Subject}}",
  "potentialAction": ${potentialAction.trim()},
  "publisher": {
    "@type": "Organization",
    "name": "Exceptionless",
    "url": "https://exceptionless.com",
    "logo": "https://be.exceptionless.io/img/exceptionless-48.png"
  }
}
`);
}
