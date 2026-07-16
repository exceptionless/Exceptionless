import { createSearchIndexLoader, searchEntries } from "../public/assets/js/site-search.js"

Deno.test("createSearchIndexLoader retries after a transient failure", async () => {
  let requestCount = 0
  const loadSearchIndex = createSearchIndexLoader(() => {
    requestCount++
    if (requestCount === 1) {
      return Promise.resolve(new Response(null, { status: 503 }))
    }

    return Promise.resolve(Response.json({ entries: [entry("Docker", "/docs/docker/", "Containers")] }))
  })

  await assertRejects(loadSearchIndex, "503")
  assertEquals((await loadSearchIndex()).map((result) => result.url), ["/docs/docker/"])
  assertEquals(requestCount, 2)
})

Deno.test("searchEntries requires every query token", () => {
  const entries = [
    entry("Docker hosting", "/docs/self-hosting/docker/", "Run the container locally"),
    entry("Kubernetes hosting", "/docs/self-hosting/kubernetes/", "Run the cluster locally"),
  ]

  assertEquals(searchEntries(entries, "docker locally").map((result) => result.url), [
    "/docs/self-hosting/docker/",
  ])
})

Deno.test("searchEntries ranks an exact title above body-only matches", () => {
  const entries = [
    entry("Client configuration", "/docs/client-configuration/", "Project tokens"),
    entry("Project tokens", "/docs/project-tokens/", "Client configuration"),
  ]

  assertEquals(searchEntries(entries, "project tokens").map((result) => result.url), [
    "/docs/project-tokens/",
    "/docs/client-configuration/",
  ])
})

Deno.test("searchEntries returns safe code-token excerpts", () => {
  const candidate = entry("Configure the client", "/docs/configure/", "Browser setup ExceptionlessClient")
  candidate.codeBlocks = [{
    language: "javascript",
    text: "const client = new ExceptionlessClient()",
    tokens: [
      { text: "const", classes: ["hljs-keyword"] },
      { text: " client = new ExceptionlessClient()" },
    ],
  }]

  const [result] = searchEntries([candidate], "ExceptionlessClient")

  assertEquals(result.codeExcerpt.language, "javascript")
  assertEquals(result.codeExcerpt.tokens.flatMap((token) => token.classes ?? []), ["hljs-keyword"])
  assertEquals(result.codeExcerpt.tokens.map((token) => token.text).join(""), candidate.codeBlocks[0].text)
})

function entry(title, url, text) {
  return { title, url, description: "", prose: text, text }
}

function assertEquals(actual, expected) {
  if (JSON.stringify(actual) !== JSON.stringify(expected)) {
    throw new Error(`Expected ${JSON.stringify(expected)}, received ${JSON.stringify(actual)}`)
  }
}

async function assertRejects(action, expectedMessage) {
  try {
    await action()
  } catch (error) {
    if (error instanceof Error && error.message.includes(expectedMessage)) {
      return
    }

    throw error
  }

  throw new Error("Expected action to reject")
}
