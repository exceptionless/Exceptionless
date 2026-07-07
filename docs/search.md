---
title: Search
description: Search Exceptionless documentation, guides, and articles.
url: /search/
layout: layouts/docs.vto
robots: noindex,follow
extraStyles:
  - /assets/css/search.css
extraModuleScripts:
  - /assets/js/search.js
---

<section data-pagefind-ignore="all">
  <h2 id="search-exceptionless">Search Exceptionless</h2>
  <p class="lead">Find documentation, client guides, release notes, and product pages.</p>

  <form id="site-search-form" class="form-search site-search-form" action="/search/" method="get">
    <label class="sr-only" for="site-search-input">Search</label>
    <input id="site-search-input" class="input-xxlarge search-query" name="q" type="search" placeholder="Search docs, clients, filtering, self-hosting..." autocomplete="off">
    <button class="btn btn-primary" type="submit">Search</button>
    <button id="site-search-clear" class="btn" type="button" hidden>Clear</button>
  </form>

  <p id="site-search-status" class="muted site-search-status" role="status" aria-live="polite">Enter a search term to begin.</p>

  <ol id="site-search-results" class="unstyled site-search-results"></ol>

  <div class="site-search-actions">
    <button id="site-search-more" class="btn" type="button" hidden>Show More</button>
  </div>
</section>
