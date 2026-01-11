(function () {
    const storageKey = "evermail-theme";
    const root = document.documentElement;

    const applyTheme = (theme) => {
        if (!root) {
            return;
        }
        root.setAttribute("data-theme", theme);
        if (document.body) {
            document.body.setAttribute("data-theme", theme);
        }
    };

    const getStoredTheme = () => {
        try {
            return window.localStorage.getItem(storageKey);
        } catch {
            return null;
        }
    };

    const setStoredTheme = (theme) => {
        try {
            window.localStorage.setItem(storageKey, theme);
        } catch {
            // Ignore storage errors (Safari private mode, etc.)
        }
    };

    const systemPrefersDark = () =>
        window.matchMedia &&
        window.matchMedia("(prefers-color-scheme: dark)").matches;

    const deriveInitialTheme = () =>
        getStoredTheme() ?? (systemPrefersDark() ? "dark" : "light");

    const setTheme = (theme) => {
        const nextTheme = theme === "dark" ? "dark" : "light";
        applyTheme(nextTheme);
        setStoredTheme(nextTheme);
        return nextTheme;
    };

    const toggleTheme = () => {
        const current = root.getAttribute("data-theme") ?? deriveInitialTheme();
        const next = current === "dark" ? "light" : "dark";
        return setTheme(next);
    };

    // Expose helpers for Blazor components
    window.EvermailTheme = {
        getTheme: () => root.getAttribute("data-theme") ?? deriveInitialTheme(),
        setTheme,
        toggleTheme
    };

    // Apply immediately on page load
    applyTheme(deriveInitialTheme());

    // React to OS-level changes only if user has not chosen a theme manually
    if (window.matchMedia) {
        const handler = (event) => {
            if (!getStoredTheme()) {
                applyTheme(event.matches ? "dark" : "light");
            }
        };

        try {
            window
                .matchMedia("(prefers-color-scheme: dark)")
                .addEventListener("change", handler);
        } catch {
            // Safari <14 fallback
            window
                .matchMedia("(prefers-color-scheme: dark)")
                .addListener(handler);
        }
    }
})();

