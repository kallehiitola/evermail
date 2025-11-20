(function () {
    const storageKey = 'evermail.preferences';
    const defaults = {
        DateFormat: 0,
        AutoScrollToKeyword: true
    };

    function sanitize(input) {
        const result = { ...defaults };
        if (input && typeof input === 'object') {
            if (typeof input.DateFormat === 'number') {
                result.DateFormat = input.DateFormat;
            } else if (typeof input.dateFormat === 'number') {
                result.DateFormat = input.dateFormat;
            }

            if (typeof input.AutoScrollToKeyword === 'boolean') {
                result.AutoScrollToKeyword = input.AutoScrollToKeyword;
            } else if (typeof input.autoScrollToKeyword === 'boolean') {
                result.AutoScrollToKeyword = input.autoScrollToKeyword;
            }
        }
        return result;
    }

    function read() {
        try {
            const raw = localStorage.getItem(storageKey);
            if (!raw) {
                return { ...defaults };
            }

            const parsed = JSON.parse(raw);
            return sanitize(parsed);
        } catch {
            return { ...defaults };
        }
    }

    function write(prefs) {
        const normalized = sanitize(prefs);
        localStorage.setItem(storageKey, JSON.stringify(normalized));
        return normalized;
    }

    window.EvermailPreferences = {
        get: () => read(),
        set: (prefs) => write(prefs),
        clear: () => localStorage.removeItem(storageKey)
    };
})();

