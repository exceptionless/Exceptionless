import { createSearchIndexLoader, initializeSiteSearch, searchEntries } from "../public/assets/js/site-search.js"

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

Deno.test("site startup restores the saved tab before the reader interacts", async () => {
  await withBrowser(async ({ document, elements, storage }) => {
    // Arrange
    const container = new TestElement()
    container.setAttribute("data-tab-sync", "")
    const panels = [new TestElement(), new TestElement()]
    container.children = [".NET", "JavaScript"].map((label, index) => {
      const tab = new TestElement()
      tab.dataset.tabLabel = label
      tab.setAttribute("aria-controls", `panel-${index}`)
      tab.setAttribute("aria-selected", String(index === 0))
      panels[index].hidden = index !== 0
      elements.set(`panel-${index}`, panels[index])
      return tab
    })
    document.selectors.set("[data-tabs]", [container])
    storage.set("docs-tab", "JavaScript")

    // Act
    await import("../public/assets/js/site.js?tab-restoration-test")

    // Assert
    assertEquals(container.children.map((tab) => tab.getAttribute("aria-selected")), ["false", "true"])
    assertEquals(panels.map((panel) => panel.hidden), [true, false])
  })
})

for (const selectionMethod of ["keyboard", "pointer"]) {
  for (const selectionTiming of ["before", "after"]) {
    const expectation = selectionTiming === "before" ? "searches the edited query" : "opens the chosen visible result"
    Deno.test(`search submit ${expectation} when ${selectionMethod} selection is ${selectionTiming} editing`, async () => {
      await withBrowser(async ({ document, flushTimers, navigations }) => {
        // Arrange
        const modal = new TestElement()
        const input = new TestElement()
        const form = new TestElement()
        const results = new TestElement()
        modal.selectors = new Map([
          ["#site-search-input", input],
          ["#site-search-clear", new TestElement()],
          ["#site-search-form", form],
          ["#site-search-results", results],
          ["#site-search-status", new TestElement()],
        ])
        document.body = new TestElement()
        initializeSiteSearch(modal)
        input.value = "hosting"
        await input.emit("input")
        await flushTimers()

        // Act
        const chooseResult = () =>
          selectionMethod === "keyboard"
            ? modal.emit("keydown", { key: "ArrowDown" })
            : results.emit("mousemove", { target: results.children[1] })
        if (selectionTiming === "before") {
          await chooseResult()
        }
        input.value = "tokens"
        await input.emit("input")
        if (selectionTiming === "after") {
          await chooseResult()
        }

        // Submit before the edited query's debounce callback runs.
        await form.emit("submit")

        // Assert
        assertEquals(navigations, [selectionTiming === "before" ? "/docs/project-tokens/" : "/docs/kubernetes/"])
      })
    })
  }
}

/** Minimal browser surfaces for running the real initialization and event handlers without network or real timers. */
async function withBrowser(action) {
  const document = new TestElement()
  document.documentElement = new TestElement()
  const elements = new Map()
  document.getElementById = (id) => elements.get(id) ?? null
  document.createElement = () => new TestElement()
  document.createTextNode = (text) => ({ textContent: text })
  const storage = new Map()
  const timers = new Map()
  const navigations = []
  let timerId = 0
  const globals = {
    document,
    Element: TestElement,
    HTMLElement: TestElement,
    HTMLInputElement: TestElement,
    HTMLButtonElement: TestElement,
    HTMLFormElement: TestElement,
    HTMLOListElement: TestElement,
    HTMLDialogElement: class extends TestElement {},
    matchMedia: undefined,
    addEventListener() {},
    localStorage: {
      getItem: (key) => storage.get(key) ?? null,
      setItem: (key, value) => storage.set(key, value),
    },
    window: {
      clearTimeout: (id) => timers.delete(id),
      setTimeout: (callback) => {
        timers.set(++timerId, callback)
        return timerId
      },
      location: { assign: (url) => navigations.push(url) },
    },
    fetch: () =>
      Promise.resolve(Response.json({
        entries: [
          entry("Docker hosting", "/docs/docker/", "Containers"),
          entry("Kubernetes hosting", "/docs/kubernetes/", "Clusters"),
          entry("Project tokens", "/docs/project-tokens/", "Authentication"),
        ],
      })),
  }
  const originals = new Map(Object.keys(globals).map((key) => [key, Object.getOwnPropertyDescriptor(globalThis, key)]))
  try {
    for (const [key, value] of Object.entries(globals)) {
      Object.defineProperty(globalThis, key, { configurable: true, writable: true, value })
    }
    await action({
      document,
      elements,
      storage,
      navigations,
      async flushTimers() {
        const callbacks = [...timers.values()]
        timers.clear()
        for (const callback of callbacks) {
          await callback()
        }
      },
    })
  } finally {
    for (const [key, descriptor] of originals) {
      if (descriptor) {
        Object.defineProperty(globalThis, key, descriptor)
      } else {
        delete globalThis[key]
      }
    }
  }
}

class TestElement {
  children = []
  dataset = {}
  selectors = new Map()
  attributes = new Map()
  listeners = new Map()
  classList = { add() {}, remove() {}, toggle() {} }

  addEventListener(type, listener) {
    this.listeners.set(type, listener)
  }
  async emit(type, properties = {}) {
    await this.listeners.get(type)?.({ target: this, preventDefault() {}, ...properties })
  }
  querySelector(selector) {
    return this.selectors.get(selector) ?? null
  }
  querySelectorAll(selector) {
    return this.selectors.get(selector) ?? this.children
  }
  setAttribute(name, value) {
    this.attributes.set(name, value)
  }
  getAttribute(name) {
    return this.attributes.get(name) ?? null
  }
  hasAttribute(name) {
    return this.attributes.has(name)
  }
  removeAttribute(name) {
    this.attributes.delete(name)
  }
  append(...children) {
    this.children.push(...children)
  }
  replaceChildren(...children) {
    this.children = children
  }
  closest() {
    return this
  }
  scrollIntoView() {}
}

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
