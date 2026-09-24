document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".card").forEach(function (card) {
        card.addEventListener("mouseenter", function () { card.style.transform = "translateY(-2px)"; });
        card.addEventListener("mouseleave", function () { card.style.transform = "translateY(0)"; });
    });

    const revealTargets = document.querySelectorAll(".card, .hero-section, .section-title");
    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = "1";
                entry.target.style.transform = "translateY(0)";
            }
        });
    }, { threshold: 0.12 });

    revealTargets.forEach((el) => {
        el.style.opacity = "0";
        el.style.transform = "translateY(18px)";
        el.style.transition = "all .45s ease";
        observer.observe(el);
    });

    document.querySelectorAll(".toggle-password").forEach((button) => {
        button.addEventListener("click", function () {
            const targetId = this.getAttribute("data-target");
            const input = document.getElementById(targetId);
            if (!input) {
                return;
            }
            const isPassword = input.getAttribute("type") === "password";
            input.setAttribute("type", isPassword ? "text" : "password");
            this.textContent = isPassword ? "Hide" : "Show";
        });
    });

    const currentPath = window.location.pathname.toLowerCase();
    const navLinks = document.querySelectorAll(".nav-route");
    navLinks.forEach((link) => {
        const href = (link.getAttribute("href") || "").toLowerCase();
        if (!href || href === "#") {
            return;
        }

        const isHome = href === "/";
        const isMatch = isHome ? currentPath === "/" : currentPath === href || currentPath.startsWith(href + "/");
        if (isMatch) {
            link.classList.add("active");
            link.setAttribute("aria-current", "page");
        }
    });

    // Ensure browser back/forward cache restores always revalidate auth state.
    window.addEventListener("pageshow", function (event) {
        if (event.persisted) {
            window.location.reload();
            return;
        }

        revealTargets.forEach((el) => {
            el.style.opacity = "1";
            el.style.transform = "translateY(0)";
        });
    });
});
