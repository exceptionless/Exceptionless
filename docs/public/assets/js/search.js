const form = requiredElement("site-search-form", HTMLFormElement)
const input = requiredElement("site-search-input", HTMLInputElement)
const clearButton = requiredElement("site-search-clear", HTMLButtonElement)
const loadMoreButton = requiredElement("site-search-more", HTMLButtonElement)
const resultsList = requiredElement("site-search-results", HTMLOListElement)
const status = requiredElement("site-search-status", HTMLElement)

const pageSize = 10
let pagefind
let searchResults = []
let renderedCount = 0
let activeSearchId = 0
let debounceTimer

const initialQuery = new URLSearchParams(location.search).get("q")?.trim() ?? ""
input.value = initialQuery

form.addEventListener("submit", (event) => {
  event.preventDefault()
  runSearch(input.value, { immediate: true, updateUrl: true })
})

input.addEventListener("input", () => {
  runSearch(input.value, { updateUrl: true })
})

clearButton.addEventListener("click", () => {
  input.value = ""
  input.focus()
  clearSearch()
  updateUrl("")
})

loadMoreButton.addEventListener("click", () => {
  void renderNextResults(activeSearchId)
})

if (initialQuery) {
  runSearch(initialQuery, { immediate: true, updateUrl: false })
}

function runSearch(query, options = {}) {
  clearTimeout(debounceTimer)
  const trimmedQuery = query.trim()

  if (!trimmedQuery) {
    clearSearch()
    updateUrl("")
    return
  }

  if (options.updateUrl) {
    updateUrl(trimmedQuery)
  }

  if (options.immediate) {
    void performSearch(trimmedQuery)
    return
  }

  debounceTimer = setTimeout(() => {
    void performSearch(trimmedQuery)
  }, 250)
}

async function performSearch(query) {
  const searchId = ++activeSearchId
  setStatus(`Searching for "${query}"...`)
  clearButton.hidden = false
  loadMoreButton.hidden = true
  resultsList.replaceChildren()

  let response
  try {
    const api = await loadPagefind()
    response = await api.search(query)
  } catch (error) {
    if (searchId === activeSearchId) {
      setStatus("Search is unavailable. Please try again.")
      console.error(error)
    }
    return
  }

  if (!response || searchId !== activeSearchId) {
    return
  }

  searchResults = response.results
  renderedCount = 0

  if (!searchResults.length) {
    setStatus(`No results found for "${query}".`)
    return
  }

  setStatus(`${searchResults.length} ${searchResults.length === 1 ? "result" : "results"} for "${query}".`)
  await renderNextResults(searchId)
}

async function loadPagefind() {
  if (pagefind) {
    return pagefind
  }

  pagefind = await import("/pagefind/pagefind.js")
  await pagefind.options({
    excerptLength: 32,
  })

  return pagefind
}

async function renderNextResults(searchId) {
  const nextResults = searchResults.slice(renderedCount, renderedCount + pageSize)
  const fragment = document.createDocumentFragment()

  for (const result of nextResults) {
    const data = await result.data()
    fragment.append(renderResult(data))
  }

  if (searchId !== activeSearchId) {
    return
  }

  resultsList.append(fragment)
  renderedCount += nextResults.length
  loadMoreButton.hidden = renderedCount >= searchResults.length
}

function renderResult(data) {
  const item = document.createElement("li")
  item.className = "site-search-result"

  const link = document.createElement("a")
  link.className = "site-search-result-title"
  link.href = data.url
  link.textContent = data.meta?.title || titleFromUrl(data.url)
  item.append(link)

  const url = document.createElement("p")
  url.className = "site-search-result-url"
  url.textContent = pathFromUrl(data.url)
  item.append(url)

  if (data.excerpt) {
    const excerpt = document.createElement("p")
    excerpt.className = "site-search-result-excerpt"
    excerpt.innerHTML = data.excerpt
    item.append(excerpt)
  }

  const subResults = data.sub_results?.filter((subResult) => subResult.url !== data.url).slice(0, 3) ?? []
  if (subResults.length) {
    const list = document.createElement("ul")
    list.className = "site-search-subresults"

    for (const subResult of subResults) {
      const subItem = document.createElement("li")
      const subLink = document.createElement("a")
      subLink.href = subResult.url
      subLink.textContent = subResult.title || titleFromUrl(subResult.url)
      subItem.append(subLink)
      list.append(subItem)
    }

    item.append(list)
  }

  return item
}

function clearSearch() {
  activeSearchId++
  searchResults = []
  renderedCount = 0
  resultsList.replaceChildren()
  clearButton.hidden = true
  loadMoreButton.hidden = true
  setStatus("Enter a search term to begin.")
}

function updateUrl(query) {
  const url = new URL(location.href)
  if (query) {
    url.searchParams.set("q", query)
  } else {
    url.searchParams.delete("q")
  }

  history.replaceState({}, "", url)
}

function setStatus(message) {
  status.textContent = message
}

function pathFromUrl(value) {
  return new URL(value, location.origin).pathname
}

function titleFromUrl(value) {
  const path = pathFromUrl(value).replace(/\/$/, "")
  const segment = path.split("/").filter(Boolean).pop() || "Exceptionless"
  return segment
    .split("-")
    .map((part) => part ? part[0].toUpperCase() + part.slice(1) : part)
    .join(" ")
}

function requiredElement(id, type) {
  const element = document.getElementById(id)
  if (element instanceof type) {
    return element
  }

  throw new Error(`Missing search page element #${id}`)
}
