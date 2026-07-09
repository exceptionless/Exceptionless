document.querySelectorAll("[data-toggle='collapse'][data-target]").forEach((toggle) => {
  toggle.addEventListener("click", (event) => {
    event.preventDefault()

    const targetSelector = toggle.getAttribute("data-target")
    if (!targetSelector) {
      return
    }

    document.querySelectorAll(targetSelector).forEach((target) => {
      target.classList.toggle("in")
    })
  })
})

document.querySelector("[data-docs-toc-toggle]")?.addEventListener("click", () => {
  const toc = document.querySelector(".toc")
  if (!toc) {
    return
  }

  toc.classList.toggle("visible-toc")
  toc.classList.toggle("hidden-toc")
})

const searchModal = document.getElementById("site-search-modal")
if (searchModal) {
  initializeSiteSearch(searchModal)
}

function initializeSiteSearch(modal) {
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
  let searchIndexPromise = null
  let visibleResults = []

  document.querySelectorAll("[data-site-search-open]").forEach((trigger) => {
    trigger.addEventListener("click", (event) => {
      event.preventDefault()
      openSearch("", trigger)
    })
  })

  document.querySelectorAll("[data-site-search-form]").forEach((searchForm) => {
    searchForm.addEventListener("submit", (event) => {
      event.preventDefault()
      const queryInput = searchForm.querySelector("[data-site-search-query]")
      const query = queryInput instanceof HTMLInputElement ? queryInput.value : ""
      openSearch(query, searchForm)
      if (queryInput instanceof HTMLInputElement) {
        queryInput.value = ""
      }
    })
  })

  document.addEventListener("keydown", (event) => {
    if (event.defaultPrevented || isTypingTarget(event.target)) {
      return
    }

    const key = event.key.toLowerCase()
    if ((key === "k" && (event.ctrlKey || event.metaKey)) || key === "/") {
      event.preventDefault()
      openSearch("", document.activeElement)
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
    if (event.target instanceof Element && event.target.closest("[data-site-search-close]")) {
      closeSearch()
    }
  })

  form.addEventListener("submit", (event) => {
    event.preventDefault()
    const activeResult = visibleResults[activeIndex]
    if (activeResult) {
      window.location.href = activeResult.url
    }
  })

  input.addEventListener("input", () => {
    window.clearTimeout(debounceTimer)
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

  function openSearch(query, sourceElement) {
    lastActiveElement = sourceElement instanceof HTMLElement ? sourceElement : document.activeElement
    modal.hidden = false
    document.body.classList.add("site-search-open")

    if (query) {
      input.value = query
      void performSearch(query)
    } else if (!input.value.trim()) {
      clearResults()
    }

    window.requestAnimationFrame(() => {
      input.focus()
      input.select()
    })
  }

  function closeSearch() {
    modal.hidden = true
    document.body.classList.remove("site-search-open")
    if (lastActiveElement instanceof HTMLElement) {
      lastActiveElement.focus()
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
      visibleResults = []
      resultsList.replaceChildren()
      setStatus("Search is unavailable. Please try again.")
      return
    }
  }

  async function loadSearchIndex() {
    if (!searchIndexPromise) {
      searchIndexPromise = fetch("/search-index.json", { headers: { accept: "application/json" } })
        .then((response) => {
          if (!response.ok) {
            throw new Error(`Search index request failed with ${response.status}`)
          }

          return response.json()
        })
        .then((data) => Array.isArray(data.entries) ? data.entries : [])
    }

    return searchIndexPromise
  }

  function renderResults(query, totalCount) {
    resultsList.replaceChildren(...visibleResults.map((result, index) => renderResult(result, index, query)))
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

    const link = document.createElement("a")
    link.className = "site-search-result-title"
    link.href = result.url
    appendHighlightedText(link, result.title, query)
    item.append(link)

    const path = document.createElement("p")
    path.className = "site-search-result-url"
    path.textContent = result.url
    item.append(path)

    const excerpt = document.createElement("p")
    excerpt.className = "site-search-result-excerpt"
    appendHighlightedText(excerpt, result.excerpt || result.description || result.text, query)
    item.append(excerpt)

    return item
  }

  function setActiveResult(index) {
    activeIndex = Number.isFinite(index) ? index : -1
    resultsList.querySelectorAll("[data-site-search-result-index]").forEach((item) => {
      const itemIndex = Number(item.dataset.siteSearchResultIndex)
      item.classList.toggle("is-active", itemIndex === activeIndex)
      if (itemIndex === activeIndex) {
        item.scrollIntoView({ block: "nearest" })
      }
    })
  }

  function clearResults() {
    activeSearchId++
    visibleResults = []
    activeIndex = -1
    clearButton.hidden = true
    resultsList.replaceChildren()
    setStatus("Start typing to search.")
  }

  function setStatus(message) {
    status.textContent = message
  }
}

function searchEntries(entries, query) {
  const tokens = tokenize(query)
  if (!tokens.length) {
    return []
  }

  const phrase = normalizeSearchText(query)
  return entries
    .map((entry) => {
      const score = scoreSearchEntry(entry, tokens, phrase)
      return score > 0 ? { ...entry, excerpt: excerptFor(entry, tokens), score } : null
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

function excerptFor(entry, tokens) {
  const source = [entry.text, entry.description]
    .filter(Boolean)
    .find((value) => {
      const normalized = normalizeSearchText(value)
      return tokens.some((token) => normalized.includes(token))
    }) || entry.description || entry.text || ""
  const normalized = normalizeSearchText(source)
  const firstMatch = tokens
    .map((token) => normalized.indexOf(token))
    .filter((index) => index >= 0)
    .sort((a, b) => a - b)[0] ?? 0

  const start = Math.max(0, firstMatch - 90)
  const end = Math.min(source.length, firstMatch + 180)
  const prefix = start > 0 ? "... " : ""
  const suffix = end < source.length ? " ..." : ""
  return `${prefix}${source.slice(start, end).trim()}${suffix}`
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
