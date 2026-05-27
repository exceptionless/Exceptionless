import { Renderer } from '@better-svelte-email/server';
import { writeFileSync, mkdirSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

// Import all templates
import UserPasswordReset from './templates/user-password-reset.svelte';
import UserEmailVerify from './templates/user-email-verify.svelte';
import EventNotice from './templates/event-notice.svelte';
import ProjectDailySummary from './templates/project-daily-summary.svelte';
import OrganizationAdded from './templates/organization-added.svelte';
import OrganizationInvited from './templates/organization-invited.svelte';
import OrganizationNotice from './templates/organization-notice.svelte';
import OrganizationPaymentFailed from './templates/organization-payment-failed.svelte';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Template registry: name → component
const templates: Record<string, any> = {
  'user-password-reset': UserPasswordReset,
  'user-email-verify': UserEmailVerify,
  'event-notice': EventNotice,
  'project-daily-summary': ProjectDailySummary,
  'organization-added': OrganizationAdded,
  'organization-invited': OrganizationInvited,
  'organization-notice': OrganizationNotice,
  'organization-payment-failed': OrganizationPaymentFailed,
};

function cleanHtml(html: string): string {
  // Remove Svelte SSR comments
  html = html.replace(/<!--\[!?-->/g, '');
  html = html.replace(/<!--]-->/g, '');
  html = html.replace(/<!--\[-->/g, '');
  html = html.replace(/<!---->/g, '');
  
  // Remove the wrapper comment that Svelte adds
  html = html.replace(/^<!--\[-->/, '');
  html = html.replace(/<!--]-->$/, '');
  
  // Extract script blocks before whitespace collapsing
  const scripts: string[] = [];
  html = html.replace(
    /<script type="application\/ld\+json">([\s\S]*?)<\/script>/g,
    (match, content) => {
      scripts.push(content);
      return `__SCRIPT_PLACEHOLDER_${scripts.length - 1}__`;
    }
  );
  
  // Clean up excessive whitespace between tags (but not in scripts)
  html = html.replace(/>\s+</g, '><');
  
  // Collapse remaining whitespace
  html = html.replace(/\n\s*/g, '');
  
  // Restore script blocks with proper formatting (newlines prevent }} being confused with Handlebars close)
  html = html.replace(/__SCRIPT_PLACEHOLDER_(\d+)__/g, (_, idx) => {
    const content = scripts[parseInt(idx)];
    // Keep the script content as-is (with newlines) to avoid }} being parsed as Handlebars
    return `<script type="application/ld+json">\n${content.trim()}\n</script>`;
  });
  
  return html.trim();
}

function validateTemplate(name: string, html: string): void {
  // Check for encoded Handlebars tokens
  if (html.includes('&#123;') || html.includes('&lbrace;') || html.includes('&#x7B;')) {
    throw new Error(`Template "${name}" has HTML-encoded curly braces! Handlebars tokens may be broken.`);
  }
  
  // Validate balanced Handlebars blocks
  const opens = (html.match(/\{\{#(if|each|unless)/g) || []).length;
  const closes = (html.match(/\{\{\/(if|each|unless)/g) || []).length;
  if (opens !== closes) {
    throw new Error(`Template "${name}" has unbalanced Handlebars blocks: ${opens} opens vs ${closes} closes`);
  }
  
  // Ensure required structure
  if (!html.includes('<!DOCTYPE html')) {
    throw new Error(`Template "${name}" missing DOCTYPE`);
  }
  if (!html.includes('{{Subject}}')) {
    throw new Error(`Template "${name}" missing {{Subject}} token`);
  }
}

async function main() {
  const renderer = new Renderer({
    tailwindConfig: {
      theme: {
        extend: {}
      }
    }
  });

  // Output directory
  const outputDir = resolve(__dirname, '..', '..', 'Exceptionless.Core', 'Mail', 'Templates');
  mkdirSync(outputDir, { recursive: true });

  console.log(`Building ${Object.keys(templates).length} email templates...`);

  for (const [name, component] of Object.entries(templates)) {
    console.log(`  Rendering: ${name}`);
    let html = await renderer.render(component);
    html = cleanHtml(html);
    validateTemplate(name, html);
    writeFileSync(resolve(outputDir, `${name}.html`), html);
  }

  console.log(`\nDone! ${Object.keys(templates).length} templates written to: ${outputDir}`);
}

main().catch((err) => {
  console.error('Build failed:', err);
  process.exit(1);
});
