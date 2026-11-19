import { type Breadcrumb, docsBreadcrumbs } from "./site-collections.ts"

type Searcher = Parameters<typeof docsBreadcrumbs>[0]

type PageData = {
  url?: string | false
  title?: string
  metaTitle?: string
  description?: string
  author?: string
  image?: string
  layout?: string
  robots?: string
  docs_nav?: unknown
}

type Plan = {
  name: string
  monthlyPrice: number
  description: string
}

const siteUrl = "https://exceptionless.com"
const organizationId = `${siteUrl}/#organization`
const websiteId = `${siteUrl}/#website`
const plans = JSON.parse(Deno.readTextFileSync("_data/plans.json")) as Plan[]
const site = JSON.parse(Deno.readTextFileSync("_data/site.json")) as {
  description: string
  githubUrl: string
  appUrl: string
}

export const defaultSocialImage = "/assets/img/social-card.png"

/** Renders the schema.org JSON-LD graph for a page so search engines and AI assistants can identify what it is. */
export function structuredData(data: PageData, search: Searcher): string {
  if (typeof data.url !== "string" || data.robots?.includes("noindex")) {
    return ""
  }

  const url = `${siteUrl}${data.url}`
  const name = data.url === "/" ? "Exceptionless" : data.metaTitle || data.title || "Exceptionless"
  const description = data.description || site.description
  const image = `${siteUrl}${data.image || defaultSocialImage}`
  const isDocs = data.url.startsWith("/docs/")
  const isPost = data.layout === "layouts/post.vto"
  const breadcrumbs = isDocs ? docsBreadcrumbs(search, data.url, data.docs_nav) : pageBreadcrumbs(data)

  const graph: Record<string, unknown>[] = [
    {
      "@type": "Organization",
      "@id": organizationId,
      name: "Exceptionless",
      url: `${siteUrl}/`,
      logo: {
        "@type": "ImageObject",
        url: `${siteUrl}/assets/img/exceptionless-logo.png`,
        width: 1200,
        height: 258,
      },
      foundingDate: "2013",
      sameAs: ["https://github.com/exceptionless", "https://x.com/exceptionless"],
    },
    {
      "@type": "WebSite",
      "@id": websiteId,
      url: `${siteUrl}/`,
      name: "Exceptionless",
      description: site.description,
      publisher: { "@id": organizationId },
      inLanguage: "en",
    },
    {
      "@type": "WebPage",
      "@id": `${url}#webpage`,
      url,
      name,
      description,
      isPartOf: { "@id": websiteId },
      primaryImageOfPage: { "@type": "ImageObject", url: image },
      inLanguage: "en",
      ...(breadcrumbs.length > 1 ? { breadcrumb: { "@id": `${url}#breadcrumb` } } : {}),
    },
  ]

  if (data.url === "/") {
    graph.push(softwareApplication(description, image))
  }

  if (isDocs) {
    graph.push({
      "@type": "TechArticle",
      "@id": `${url}#article`,
      headline: data.title,
      description,
      url,
      image,
      mainEntityOfPage: { "@id": `${url}#webpage` },
      author: { "@id": organizationId },
      publisher: { "@id": organizationId },
      isPartOf: { "@id": websiteId },
      about: { "@id": `${siteUrl}/#software` },
      inLanguage: "en",
    })
  }

  if (isPost) {
    graph.push({
      "@type": "BlogPosting",
      "@id": `${url}#article`,
      headline: data.title,
      description,
      url,
      image,
      mainEntityOfPage: { "@id": `${url}#webpage` },
      author: data.author ? { "@type": "Person", name: data.author } : { "@id": organizationId },
      publisher: { "@id": organizationId },
      isPartOf: { "@id": websiteId },
      inLanguage: "en",
    })
  }

  if (breadcrumbs.length > 1) {
    graph.push({
      "@type": "BreadcrumbList",
      "@id": `${url}#breadcrumb`,
      itemListElement: breadcrumbs.map((crumb, index) => ({
        "@type": "ListItem",
        position: index + 1,
        name: crumb.name,
        item: `${siteUrl}${crumb.url}`,
      })),
    })
  }

  const json = JSON.stringify({ "@context": "https://schema.org", "@graph": graph })
  return `<script type="application/ld+json">${json.replaceAll("</", "<\\/")}</script>`
}

function softwareApplication(description: string, image: string): Record<string, unknown> {
  return {
    "@type": "SoftwareApplication",
    "@id": `${siteUrl}/#software`,
    name: "Exceptionless",
    url: `${siteUrl}/`,
    description,
    image,
    applicationCategory: "DeveloperApplication",
    applicationSubCategory: "Error monitoring",
    operatingSystem: "Web, Windows, Linux, macOS",
    softwareHelp: { "@type": "CreativeWork", url: `${siteUrl}/docs/` },
    license: "https://www.apache.org/licenses/LICENSE-2.0",
    codeRepository: site.githubUrl,
    publisher: { "@id": organizationId },
    featureList: [
      "Error and crash reporting with automatic grouping into stacks",
      "Real-time notifications for new and regressed errors",
      "Structured logs, feature usage, and user session tracking",
      "Exie, an AI assistant that investigates and triages errors",
      "MCP server for AI coding agents such as Claude Code, Codex, GitHub Copilot, and OpenCode, on every plan",
      "Clients for .NET, JavaScript, Node.js, and React Native",
      "Hosted service or self-hosted with Docker and Kubernetes",
    ],
    offers: plans.map((plan) => ({
      "@type": "Offer",
      name: plan.name,
      description: plan.description,
      price: plan.monthlyPrice.toFixed(2),
      priceCurrency: "USD",
      url: `${siteUrl}/pricing/`,
      ...(plan.monthlyPrice > 0
        ? {
          priceSpecification: {
            "@type": "UnitPriceSpecification",
            price: plan.monthlyPrice.toFixed(2),
            priceCurrency: "USD",
            unitCode: "MON",
          },
        }
        : {}),
    })),
  }
}

function pageBreadcrumbs(data: PageData): Breadcrumb[] {
  if (typeof data.url !== "string" || data.url === "/") {
    return []
  }

  const crumbs: Breadcrumb[] = [{ name: "Home", url: "/" }]
  if (data.layout === "layouts/post.vto") {
    crumbs.push({ name: "News", url: "/news/" })
  }

  crumbs.push({ name: data.title || "Exceptionless", url: data.url })
  return crumbs
}
