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
