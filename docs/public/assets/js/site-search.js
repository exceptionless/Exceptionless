export function initializeSiteSearch(modal) {
  const input = modal.querySelector("#site-search-input")
  const clearButton = modal.querySelector("#site-search-clear")
  const form = modal.querySelector("#site-search-form")
  const resultsList = modal.querySelector("#site-search-results")
  const status = modal.querySelector("#site-search-status")

  if (
    !(input instanceof HTMLInputElement) ||
    !(clearButton instanceof HTMLButtonElement) ||
    !(form instanceof HTMLFormElement) ||
    !(resultsList instanceof HTMLOListElement) ||
    !(status instanceof HTMLElement)
  ) {
    return
  }

  let activeIndex = -1
  let activeSearchId = 0
  let debounceTimer = 0
  let lastActiveElement = null
  const loadSearchIndex = createSearchIndexLoader()
  let visibleResults = []
  let visibleResultsQuery = ""

  document.querySelectorAll("[data-site-search-open]").forEach((trigger) => {
    trigger.addEventListener("click", (event) => {
      event.preventDefault()
      openSearch(trigger)
    })
  })

  document.addEventListener("keydown", (event) => {
    if (event.defaultPrevented || isTypingTarget(event.target)) {
      return
    }

    const key = event.key.toLowerCase()
    if ((key === "k" && (event.ctrlKey || event.metaKey)) || key === "/") {
      event.preventDefault()
      openSearch(document.activeElement)
    }
  })

  modal.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      event.preventDefault()
      closeSearch()
      return
    }

    if (event.key === "ArrowDown") {
      event.preventDefault()
      setActiveResult(Math.min(activeIndex + 1, visibleResults.length - 1))
      return
    }

    if (event.key === "ArrowUp") {
      event.preventDefault()
      setActiveResult(Math.max(activeIndex - 1, 0))
    }
  })

  modal.addEventListener("click", (event) => {
    if (event.target === modal) {
      closeSearch()
    }
  })

  modal.addEventListener("close", () => {
    document.body.classList.remove("site-search-open")
    if (lastActiveElement instanceof HTMLElement) {
      lastActiveElement.focus()
    }
  })

  form.addEventListener("submit", (event) => {
    event.preventDefault()
    const currentQuery = input.value.trim()
    if (!currentQuery) {
      clearResults()
      return
    }

    if (visibleResultsQuery !== currentQuery) {
      window.clearTimeout(debounceTimer)
      void performSearch(currentQuery)
      return
    }

    const activeResult = visibleResults[activeIndex]
    if (activeResult) {
      window.location.href = activeResult.url
    }
  })

  input.addEventListener("input", () => {
    window.clearTimeout(debounceTimer)
    activeSearchId++
    const query = input.value.trim()
    if (!query) {
      clearResults()
      return
    }

    clearButton.hidden = false
    setStatus(`Searching for "${query}"...`)
    debounceTimer = window.setTimeout(() => performSearch(input.value), 120)
  })

  clearButton.addEventListener("click", () => {
    window.clearTimeout(debounceTimer)
    input.value = ""
    input.focus()
    clearResults()
  })

  resultsList.addEventListener("mousemove", (event) => {
    const item = event.target instanceof Element ? event.target.closest("[data-site-search-result-index]") : null
    if (!(item instanceof HTMLElement)) {
      return
    }

    setActiveResult(Number(item.dataset.siteSearchResultIndex))
  })

  function openSearch(sourceElement) {
    lastActiveElement = sourceElement instanceof HTMLElement ? sourceElement : document.activeElement
    if (modal instanceof HTMLDialogElement && !modal.open) {
      modal.showModal()
    }
    document.body.classList.add("site-search-open")

    if (!input.value.trim()) {
      clearResults()
    }

    window.requestAnimationFrame(() => {
      input.focus()
      input.select()
    })
  }

  function closeSearch() {
    if (modal instanceof HTMLDialogElement && modal.open) {
      modal.close()
    }
  }

  async function performSearch(query) {
    const searchId = ++activeSearchId
    const trimmedQuery = query.trim()
    clearButton.hidden = !trimmedQuery

    if (!trimmedQuery) {
      clearResults()
      return
    }

    setStatus(`Searching for "${trimmedQuery}"...`)

    try {
      const entries = await loadSearchIndex()
      if (searchId !== activeSearchId) {
        return
      }

      const matches = searchEntries(entries, trimmedQuery)
      visibleResults = matches.slice(0, 30)
      renderResults(trimmedQuery, matches.length)
    } catch (error) {
      if (searchId !== activeSearchId) {
        return
      }

      console.error(error)
      resetResults("Search is unavailable. Please try again.", false)
      return
    }
  }

  function renderResults(query, totalCount) {
    visibleResultsQuery = query.trim()
    resultsList.replaceChildren(...visibleResults.map((result, index) => renderResult(result, index, query)))
    input.setAttribute("aria-expanded", String(visibleResults.length > 0))
    setActiveResult(visibleResults.length ? 0 : -1)

    if (!visibleResults.length) {
      setStatus(`No results found for "${query}".`)
      return
    }

    if (totalCount > visibleResults.length) {
      setStatus(`Top ${visibleResults.length} of ${totalCount} results for "${query}".`)
      return
    }

    setStatus(`${visibleResults.length} ${visibleResults.length === 1 ? "result" : "results"} for "${query}".`)
  }

  function renderResult(result, index, query) {
    const item = document.createElement("li")
    item.className = "site-search-result"
    item.dataset.siteSearchResultIndex = String(index)
    item.id = `site-search-result-${index}`
    item.setAttribute("role", "option")
    item.setAttribute("aria-selected", "false")

    const link = document.createElement("a")
    link.className = "site-search-result-title"
    link.href = result.url
    appendHighlightedText(link, result.title, query)
    item.append(link)

    const metadata = document.createElement("p")
    metadata.className = "site-search-result-metadata"

    const type = document.createElement("span")
    type.className = "site-search-result-type"
    type.textContent = resultType(result.url)
    metadata.append(type)

    const path = document.createElement("span")
    path.className = "site-search-result-path"
    path.textContent = formatResultPath(result.url)
    path.title = result.url
    metadata.append(path)
    item.append(metadata)

    if (result.excerpt) {
      const excerpt = document.createElement("p")
      excerpt.className = "site-search-result-excerpt"
      appendHighlightedText(excerpt, result.excerpt, query)
      item.append(excerpt)
    }

    if (result.codeExcerpt) {
      item.append(renderCodeExcerpt(result.codeExcerpt, query))
    }

    return item
  }

  function renderCodeExcerpt(excerpt, query) {
    const pre = document.createElement("pre")
    pre.className = `site-search-result-code language-${excerpt.language}`

    const code = document.createElement("code")
    code.className = `hljs language-${excerpt.language}`
    if (excerpt.hasLeadingContent) {
      code.append(document.createTextNode("…\n"))
    }

    for (const token of excerpt.tokens) {
      const target = token.classes?.length ? document.createElement("span") : code
      if (target !== code) {
        target.classList.add(...token.classes)
      }

      appendHighlightedText(target, token.text, query)
      if (target !== code) {
        code.append(target)
      }
    }

    if (excerpt.hasTrailingContent) {
      code.append(document.createTextNode("\n…"))
    }

    pre.append(code)
    return pre
  }

  function setActiveResult(index) {
    activeIndex = Number.isFinite(index) ? index : -1
    resultsList.querySelectorAll("[data-site-search-result-index]").forEach((item) => {
      const itemIndex = Number(item.dataset.siteSearchResultIndex)
      const isActive = itemIndex === activeIndex
      item.classList.toggle("is-active", isActive)
      item.setAttribute("aria-selected", String(isActive))
      if (isActive) {
        input.setAttribute("aria-activedescendant", item.id)
        item.scrollIntoView({ block: "nearest" })
      }
    })

    if (activeIndex < 0) {
      input.removeAttribute("aria-activedescendant")
    }
  }

  function clearResults() {
    resetResults("Start typing to search.", true)
  }

  function resetResults(message, hideClearButton) {
    activeSearchId++
    visibleResults = []
    visibleResultsQuery = ""
    activeIndex = -1
    input.removeAttribute("aria-activedescendant")
    input.setAttribute("aria-expanded", "false")
    clearButton.hidden = hideClearButton
    resultsList.replaceChildren()
    setStatus(message)
  }

  function setStatus(message) {
    status.textContent = message
  }

}

export function createSearchIndexLoader(request = fetch) {
  let searchIndexPromise = null

  return async function loadSearchIndex() {
    if (!searchIndexPromise) {
      searchIndexPromise = request("/search-index.json", { headers: { accept: "application/json" } })
        .then((response) => {
          if (!response.ok) {
            throw new Error(`Search index request failed with ${response.status}`)
          }

          return response.json()
        })
        .then((data) => Array.isArray(data?.entries) ? data.entries : [])
        .catch((error) => {
          searchIndexPromise = null
          throw error
        })
    }

    return searchIndexPromise
  }
}

export function searchEntries(entries, query) {
  const tokens = tokenize(query)
  if (!tokens.length) {
    return []
  }

  const phrase = normalizeSearchText(query)
  return entries
    .map((entry) => {
      const score = scoreSearchEntry(entry, tokens, phrase)
      if (score <= 0) {
        return null
      }

      const codeExcerpt = codeExcerptFor(entry.codeBlocks || [], tokens)
      return { ...entry, excerpt: excerptFor(entry, tokens, Boolean(codeExcerpt)), codeExcerpt, score }
    })
    .filter(Boolean)
    .sort((a, b) => b.score - a.score || a.title.localeCompare(b.title))
}

function scoreSearchEntry(entry, tokens, phrase) {
  const title = normalizeSearchText(entry.title)
  const description = normalizeSearchText(entry.description)
  const text = normalizeSearchText(entry.text)
  const url = normalizeSearchText(entry.url)
  const haystack = `${title} ${description} ${text} ${url}`

  if (!tokens.every((token) => haystack.includes(token))) {
    return 0
  }

  let score = 0
  if (title === phrase) {
    score += 100
  } else if (title.includes(phrase)) {
    score += 70
  }

  if (url.includes(phrase)) {
    score += 30
  }

  if (entry.url?.startsWith("/docs/")) {
    score += 18
  }

  for (const token of tokens) {
    if (title.split(/\s+/).includes(token)) {
      score += 32
    } else if (title.includes(token)) {
      score += 24
    }

    if (description.includes(token)) {
      score += 10
    }

    if (url.includes(token)) {
      score += 8
    }

    if (text.includes(token)) {
      score += 3
    }
  }

  return score
}

function excerptFor(entry, tokens, hasCodeExcerpt) {
  const source = [entry.description, entry.prose]
    .filter(Boolean)
    .find((value) => {
      const normalized = normalizeSearchText(value)
      return tokens.some((token) => normalized.includes(token))
    }) || entry.description || entry.prose || (hasCodeExcerpt ? "" : entry.text) || ""
  if (!source) {
    return ""
  }

  const normalized = normalizeSearchText(source)
  const firstMatch = tokens
    .map((token) => normalized.indexOf(token))
    .filter((index) => index >= 0)
    .sort((a, b) => a - b)[0] ?? 0

  const approximateStart = Math.max(0, firstMatch - 90)
  const approximateEnd = Math.min(source.length, firstMatch + 180)
  const start = approximateStart > 0 ? nextWordBoundary(source, approximateStart) : 0
  const end = approximateEnd < source.length ? previousWordBoundary(source, approximateEnd) : source.length
  const prefix = start > 0 ? "... " : ""
  const suffix = end < source.length ? " ..." : ""
  return `${prefix}${source.slice(start, end).trim()}${suffix}`
}

function codeExcerptFor(blocks, queryTokens) {
  const exactBlock = blocks.find((candidate) => {
    const text = normalizeSearchText(candidate.text)
    return queryTokens.every((token) => text.includes(token))
  })
  const block = exactBlock || (queryTokens.length === 1
    ? blocks.find((candidate) => {
      const text = normalizeSearchText(candidate.text)
      return queryTokens.some((token) => text.includes(token))
    })
    : null)

  if (!block) {
    return null
  }

  const normalized = normalizeSearchText(block.text)
  const firstMatch = queryTokens
    .map((token) => normalized.indexOf(token))
    .filter((index) => index >= 0)
    .sort((left, right) => left - right)[0] ?? 0
  const lineRanges = codeLineRanges(block.text)
  const matchLine = Math.max(0, lineRanges.findIndex((range) => firstMatch < range.end))
  let start = lineRanges[Math.max(0, matchLine - 1)]?.start ?? 0
  let end = lineRanges[Math.min(lineRanges.length - 1, matchLine + 3)]?.end ?? block.text.length

  if (end - start > 700) {
    start = Math.max(start, firstMatch - 180)
    end = Math.min(end, firstMatch + 520)
  }

  return {
    language: block.language,
    tokens: sliceCodeTokens(block.tokens, start, end),
    hasLeadingContent: start > 0,
    hasTrailingContent: end < block.text.length,
  }
}

function codeLineRanges(value) {
  const ranges = []
  let start = 0
  for (let index = 0; index < value.length; index++) {
    if (value[index] === "\n") {
      ranges.push({ start, end: index })
      start = index + 1
    }
  }

  ranges.push({ start, end: value.length })
  return ranges
}

function sliceCodeTokens(tokens, start, end) {
  const sliced = []
  let offset = 0

  for (const token of tokens) {
    const tokenStart = offset
    const tokenEnd = offset + token.text.length
    offset = tokenEnd
    if (tokenEnd <= start || tokenStart >= end) {
      continue
    }

    const text = token.text.slice(Math.max(0, start - tokenStart), Math.min(token.text.length, end - tokenStart))
    if (text) {
      sliced.push({ text, ...(token.classes?.length ? { classes: token.classes } : {}) })
    }
  }

  return sliced
}

function nextWordBoundary(value, index) {
  const boundary = value.indexOf(" ", index)
  return boundary >= 0 ? boundary + 1 : index
}

function previousWordBoundary(value, index) {
  const boundary = value.lastIndexOf(" ", index)
  return boundary >= 0 ? boundary : index
}

function resultType(url) {
  if (url.startsWith("/docs/")) {
    return "Docs"
  }

  if (url.startsWith("/news/")) {
    return "News"
  }

  return "Site"
}

function formatResultPath(url) {
  const segments = url
    .split(/[?#]/, 1)[0]
    .split("/")
    .filter(Boolean)

  if (segments[0] === "docs" || segments[0] === "news") {
    segments.shift()
  }

  return segments.length ? segments.map(formatPathSegment).join(" › ") : "Home"
}

function formatPathSegment(segment) {
  const decoded = decodeURIComponent(segment).replace(/[-_]+/g, " ")
  const knownNames = {
    api: "API",
    faq: "FAQ",
    javascript: "JavaScript",
  }

  return knownNames[decoded.toLowerCase()] ?? decoded.replace(/\b\w/g, (character) => character.toUpperCase())
}

function appendHighlightedText(parent, text, query) {
  const tokens = tokenize(query)
  if (!tokens.length) {
    parent.textContent = text
    return
  }

  const pattern = new RegExp(`(${tokens.map(escapeRegExp).join("|")})`, "gi")
  for (const part of text.split(pattern)) {
    if (!part) {
      continue
    }

    if (tokens.includes(part.toLowerCase())) {
      const mark = document.createElement("mark")
      mark.textContent = part
      parent.append(mark)
    } else {
      parent.append(document.createTextNode(part))
    }
  }
}

function tokenize(value) {
  return [...new Set(normalizeSearchText(value).match(/[a-z0-9#.+-]+/g) ?? [])]
}

function normalizeSearchText(value) {
  return String(value ?? "").toLowerCase()
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")
}

function isTypingTarget(target) {
  return target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement ||
    (target instanceof HTMLElement && target.isContentEditable)
}
