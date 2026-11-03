import { assert, assertEquals, assertStringIncludes, assertThrows } from "./test-helpers.ts"
import { markdownIt, markdownItAttrs, markdownItDeflist } from "lume/deps/markdown_it.ts"
import {
  type Client,
  clientsSentence,
  expandClientBlocks,
  type FeatureDefinition,
  installComment,
  validateClients,
} from "./clients.ts"
import { docsMarkdown, parseFenceInfo } from "./docs-markdown.ts"

const icons = {
  exie: "<svg>exie</svg>",
  terminal: "<svg>terminal</svg>",
  copy: "<svg>copy</svg>",
  info: "<svg>info</svg>",
}

const features: FeatureDefinition[] = [
  { id: "logs", label: "Log messages", description: "Sends logs." },
  { id: "sessions", label: "Sessions", description: "Sends sessions." },
]

const clients: Client[] = [
  {
    id: "dotnet",
    language: ".NET",
    name: "Exceptionless.Net",
    support: "Official",
    repo: "https://example.com/net",
    docsUrl: "/docs/clients/dotnet/",
    version: "6.2.0",
    minimumRuntime: ".NET 8",
    summary: "ASP.NET Core and console apps",
    packages: [],
    snippets: [{
      label: ".NET",
      package: "Exceptionless",
      install: "dotnet add package Exceptionless",
      language: "csharp",
      code: 'ExceptionlessClient.Default.Startup("YOUR_API_KEY");',
      caption: "Works in console apps.",
      docsUrl: "/docs/clients/dotnet/console/",
    }],
    features: { logs: { status: "yes" }, sessions: { status: "opt-in", note: "Call UseSessions() | now" } },
  },
  {
    id: "javascript",
    language: "JavaScript",
    name: "Exceptionless.JavaScript",
    support: "Official",
    repo: "https://example.com/js",
    docsUrl: "/docs/clients/javascript/",
    version: "3.2.1",
    minimumRuntime: "ES2022",
    summary: "browser and Node.js",
    packages: [],
    snippets: [{
      label: "Browser",
      package: "@exceptionless/browser",
      install: "npm install @exceptionless/browser",
      language: "js",
      code: 'await Exceptionless.startup("YOUR_API_KEY");',
      caption: "Source maps are supported.",
      docsUrl: "/docs/clients/javascript/guides/browser/",
    }],
    features: { logs: { status: "yes" }, sessions: { status: "n/a" } },
  },
]

function render(source: string, env: Record<string, unknown> = {}): string {
  const md = markdownIt({ html: true }).use(markdownItAttrs).use(markdownItDeflist)
  docsMarkdown(md, { icons, clients, features, plans: ["Free", "Medium"] })
  return md.render(source, env)
}

Deno.test("headings: explicit ids are applied and the suffix is stripped", () => {
  const html = render("## Client configuration {#client-configuration}\n\n## Plain heading")
  assertStringIncludes(html, '<h2 id="client-configuration">Client configuration</h2>')
  assertStringIncludes(html, '<h2 id="plain-heading">Plain heading</h2>')
  assert(!html.includes("{#"))
})

Deno.test("headings: automatic ids are unique and never reuse an explicit id", () => {
  const html = render("## Setup {#setup}\n\n## Setup\n\n## Setup\n\n## Docs v1.2")
  assertStringIncludes(html, '<h2 id="setup">Setup</h2>')
  assertStringIncludes(html, '<h2 id="setup-1">Setup</h2>')
  assertStringIncludes(html, '<h2 id="setup-2">Setup</h2>')
  assertStringIncludes(html, 'id="docs-v12"')
})

Deno.test("alerts: each kind renders a labeled callout with an icon", () => {
  for (
    const [marker, kind, label] of [["NOTE", "note", "Note"], ["TIP", "tip", "Tip"], [
      "IMPORTANT",
      "important",
      "Important",
    ], ["WARNING", "warning", "Warning"]]
  ) {
    const html = render(`> [!${marker}]\n> Be careful with **bold** text.`)
    assertStringIncludes(html, `<div class="callout callout-${kind}" role="note">`)
    assertStringIncludes(html, `<p class="callout-title">`)
    assertStringIncludes(html, `<span>${label}</span>`)
    assertStringIncludes(html, "<strong>bold</strong>")
    assert(!html.includes("[!"), html)
    assert(!html.includes("<blockquote"), html)
  }
})

Deno.test("alerts: a marker-only first line and later paragraphs are kept", () => {
  const html = render("> [!TIP]\n>\n> First paragraph.\n>\n> Second paragraph.")
  assertStringIncludes(html, "callout-tip")
  assertStringIncludes(html, "<p>First paragraph.</p>")
  assertStringIncludes(html, "<p>Second paragraph.</p>")
  assert(!html.includes("<p></p>"))
  assertEquals(html.match(/<\/div>/g)?.length, 1)
})

Deno.test("alerts: ordinary blockquotes are unchanged", () => {
  const html = render("> Just a quote\n> [!NOTE] not at the start of a line")
  assertStringIncludes(html, "<blockquote>")
  assert(!html.includes("callout"))
})

Deno.test("prompts: exie and agent prompts render cards with a copy button", () => {
  const exie = render("```prompt exie\nWhich <new> stacks appeared since the deploy?\n```")
  assertStringIncludes(exie, 'class="prompt prompt-exie"')
  assertStringIncludes(exie, "<span>Ask Exie</span>")
  assertStringIncludes(exie, "<svg>exie</svg>")
  assertStringIncludes(exie, 'data-copy-target="prompt-1-text"')
  assertStringIncludes(exie, 'id="prompt-1-text">Which &lt;new&gt; stacks appeared since the deploy?</div>')
  assertStringIncludes(exie, 'aria-label="Copy prompt"')

  const agent = render("```prompt agent\nFix my top error.\n```\n\n```prompt exie\nSecond.\n```")
  assertStringIncludes(agent, "<span>Paste into your coding agent</span>")
  assertStringIncludes(agent, "<svg>terminal</svg>")
  assertStringIncludes(agent, 'id="prompt-2-text"')
})

Deno.test("prompts: an unknown kind fails the build", () => {
  assertThrows(() => render("```prompt robot\nHi\n```"), Error, "Prompt blocks need a kind")
  assertThrows(() => render("```prompt\nHi\n```"), Error, "Prompt blocks need a kind")
})

Deno.test("tabs: adjacent labeled fences group into one accessible tab set", () => {
  const html = render(
    '```csharp tab=".NET"\nvar a = 1;\n```\n\n```ts tab="JavaScript"\nconst a = 1\n```\n\n```bash tab="curl"\ncurl x\n```',
  )
  assertEquals(html.match(/data-tab-sync/g)?.length, 1)
  assertEquals(html.match(/role="tab"/g)?.length, 3)
  assertEquals(html.match(/role="tabpanel"/g)?.length, 3)
  assertStringIncludes(html, 'role="tablist"')
  assertStringIncludes(html, 'id="tabs-1-tab-0" aria-controls="tabs-1-panel-0" aria-selected="true"')
  assertStringIncludes(html, 'id="tabs-1-tab-1" aria-controls="tabs-1-panel-1" aria-selected="false" tabindex="-1"')
  assertStringIncludes(html, 'data-tab-label="JavaScript"')
  // Panels keep their language class for highlighting and a visible label for readers without JavaScript.
  assertStringIncludes(html, '<code class="language-csharp">var a = 1;')
  assertStringIncludes(html, '<code class="language-ts">const a = 1')
  assertStringIncludes(html, '<p class="tab-label">curl</p>')
  assertEquals(html.match(/ hidden>/g)?.length, 2)
  assert(!html.includes("tab=&quot;"), html)
})

Deno.test("tabs: separate groups stay separate and unlabeled fences break a group", () => {
  const html = render(
    '```csharp tab="A"\n1\n```\n\n```js tab="B"\n2\n```\n\ntext\n\n```csharp tab="A"\n3\n```\n\n```js tab="B"\n4\n```\n\n```js\nplain\n```',
  )
  assertEquals(html.match(/data-tab-sync/g)?.length, 2)
  assertStringIncludes(html, 'id="tabs-2-tab-0"')
  assertStringIncludes(html, '<pre><code class="language-js">plain')
})

Deno.test("tabs: one labeled fence is a plain code block, duplicate labels fail", () => {
  const single = render('```csharp tab=".NET"\nvar a = 1;\n```')
  assert(!single.includes("data-tabs"))
  assertStringIncludes(single, '<code class="language-csharp">')
  assertThrows(
    () => render('```js tab="A"\n1\n```\n\n```ts tab="A"\n2\n```'),
    Error,
    'share the tab label "A"',
  )
})

Deno.test("fence info: language, words, and attributes", () => {
  assertEquals(parseFenceInfo('csharp tab=".NET Core"'), {
    language: "csharp",
    words: [],
    attributes: { tab: ".NET Core" },
  })
  assertEquals(parseFenceInfo("prompt exie"), { language: "prompt", words: ["exie"], attributes: {} })
  assertEquals(parseFenceInfo('tab="X"'), { language: "", words: [], attributes: { tab: "X" } })
})

Deno.test("clients block: renders install commands and snippets for every client", () => {
  const html = render("```clients\n```")
  assertEquals(html.match(/role="tab"/g)?.length, 2)
  assertStringIncludes(html, 'data-tab-label="Browser"')
  assertStringIncludes(html, '<code class="language-bash">dotnet add package Exceptionless')
  assertStringIncludes(
    html,
    '<code class="language-csharp">ExceptionlessClient.Default.Startup(&quot;YOUR_API_KEY&quot;);',
  )
  assertStringIncludes(html, 'href="/docs/clients/javascript/guides/browser/"')
})

Deno.test("clients features block: renders the feature matrix", () => {
  const html = render("```clients features\n```")
  assertStringIncludes(html, '<table class="feature-matrix">')
  assertStringIncludes(html, '<th scope="row">Log messages</th>')
  assertStringIncludes(html, "Opt-in")
  assertStringIncludes(html, "Call UseSessions() | now")
  assertStringIncludes(html, "Not applicable")
})

Deno.test("clients block: content or unknown words fail the build", () => {
  assertThrows(() => render("```clients\nsomething\n```"), Error, "must be empty")
  assertThrows(() => render("```clients nope\n```"), Error, "Unknown clients block")
})

Deno.test("markdown copy: clients blocks expand to real Markdown, everything else is untouched", () => {
  const source = [
    "# Title",
    "",
    "```clients",
    "```",
    "",
    "```clients features",
    "```",
    "",
    "```prompt exie",
    "Ask something",
    "```",
    "",
    "> [!NOTE]",
    "> Keep this",
    "",
    '```csharp tab=".NET"',
    "var a = 1;",
    "```",
  ].join("\n")
  const markdown = expandClientBlocks(source, clients, features)
  assert(!markdown.includes("```clients"), markdown)
  assertStringIncludes(markdown, "**.NET** (package `Exceptionless`)")
  assertStringIncludes(markdown, "```bash\ndotnet add package Exceptionless\n```")
  assertStringIncludes(markdown, '```csharp\nExceptionlessClient.Default.Startup("YOUR_API_KEY");\n```')
  assertStringIncludes(markdown, "| Feature | .NET 6.2.0 | JavaScript 3.2.1 |")
  assertStringIncludes(markdown, "| Sessions | Opt-in (Call UseSessions() \\| now) | Not applicable |")
  assertStringIncludes(markdown, "```prompt exie\nAsk something\n```")
  assertStringIncludes(markdown, "> [!NOTE]\n> Keep this")
  assertStringIncludes(markdown, '```csharp tab=".NET"')
})

Deno.test("plan badge: renders under the title and rejects unknown plans", () => {
  const html = render("# Exie\n\nText", { filename: "docs/ai/exie.md", data: { plan: "Medium" } })
  assertStringIncludes(html, '<a class="docs-plan" href="/pricing/">Medium plan and up</a>')
  assert(html.indexOf("</h1>") < html.indexOf("docs-plan"))
  assertThrows(
    () => render("# Exie", { filename: "docs/ai/exie.md", data: { plan: "Gold" } }),
    Error,
    'plan "Gold" is not in _data/plans.json',
  )
  assert(!render("# Exie").includes("docs-plan"))
})

Deno.test("client data: validation catches missing features and bad support levels", () => {
  validateClients(clients, features)
  const missing = structuredClone(clients)
  delete missing[0].features.sessions
  assertThrows(() => validateClients(missing, features), Error, 'valid status for feature "sessions"')
  const bad = structuredClone(clients)
  bad[0].support = "Maybe" as Client["support"]
  assertThrows(() => validateClients(bad, features), Error, "unknown support level")
})

Deno.test("client data: the real files pass validation", async () => {
  const realClients = JSON.parse(await Deno.readTextFile("_data/clients.json")) as Client[]
  const realFeatures = JSON.parse(await Deno.readTextFile("_data/client_features.json")) as FeatureDefinition[]
  validateClients(realClients, realFeatures)
})

Deno.test("install comments wrap long commands like a shell would", () => {
  assertEquals(installComment("dotnet add package Exceptionless", "csharp"), "// dotnet add package Exceptionless")
  assertEquals(
    installComment("npx expo install @exceptionless/react-native @react-native-async-storage/async-storage", "js"),
    "// npx expo install @exceptionless/react-native \\\n//   @react-native-async-storage/async-storage",
  )
})

Deno.test("clients sentence names every client and links its docs", () => {
  assertEquals(
    clientsSentence(clients),
    'Exceptionless has clients for <a href="/docs/clients/dotnet/">.NET</a> (ASP.NET Core and console apps) and <a href="/docs/clients/javascript/">JavaScript</a> (browser and Node.js).',
  )
})
