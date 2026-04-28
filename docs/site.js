(() => {
  const storageKey = "flowencode-theme";
  const themeButtons = document.querySelectorAll("[data-theme-choice]");

  const applyTheme = (theme) => {
    const normalizedTheme = theme === "light" || theme === "dark" ? theme : "system";
    if (normalizedTheme === "system") {
      document.documentElement.removeAttribute("data-theme");
    } else {
      document.documentElement.setAttribute("data-theme", normalizedTheme);
    }

    themeButtons.forEach((button) => {
      button.setAttribute("aria-pressed", String(button.dataset.themeChoice === normalizedTheme));
    });
  };

  applyTheme(window.localStorage.getItem(storageKey));

  themeButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const nextTheme = button.dataset.themeChoice || "system";
      if (nextTheme === "system") {
        window.localStorage.removeItem(storageKey);
      } else {
        window.localStorage.setItem(storageKey, nextTheme);
      }
      applyTheme(nextTheme);
    });
  });

  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  const updatePointer = (event) => {
    const x = `${event.clientX}px`;
    const y = `${event.clientY}px`;
    document.documentElement.style.setProperty("--mx", x);
    document.documentElement.style.setProperty("--my", y);
  };

  window.addEventListener("pointermove", updatePointer, { passive: true });

  const heroMedia = document.querySelector(".hero-media");
  if (heroMedia && !reduceMotion) {
    heroMedia.addEventListener("pointermove", (event) => {
      const rect = heroMedia.getBoundingClientRect();
      const x = (event.clientX - rect.left) / rect.width - 0.5;
      const y = (event.clientY - rect.top) / rect.height - 0.5;
      heroMedia.style.setProperty("--tilt-x", `${x * 2}deg`);
      heroMedia.style.setProperty("--tilt-y", `${y * -2}deg`);
    });

    heroMedia.addEventListener("pointerleave", () => {
      heroMedia.style.setProperty("--tilt-x", "0deg");
      heroMedia.style.setProperty("--tilt-y", "0deg");
    });
  }

  const revealTargets = document.querySelectorAll(".section, .feature-grid article, .steps li, .tool-list span");
  revealTargets.forEach((target) => target.classList.add("reveal"));

  if (reduceMotion || !("IntersectionObserver" in window)) {
    revealTargets.forEach((target) => target.classList.add("is-visible"));
    return;
  }

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.12 }
  );

  revealTargets.forEach((target) => observer.observe(target));
})();
