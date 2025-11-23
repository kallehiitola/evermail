window.evermailDateTime = window.evermailDateTime || {
    format(element, isoString, options) {
        if (!element || !isoString) {
            return;
        }

        try {
            const date = new Date(isoString);
            if (Number.isNaN(date.getTime())) {
                element.textContent = isoString;
                return;
            }

            const resolvedOptions = options ?? { dateStyle: 'medium', timeStyle: 'short' };
            const formatter = new Intl.DateTimeFormat(undefined, resolvedOptions);
            element.textContent = formatter.format(date);
            element.dateTime = date.toISOString();
        } catch (error) {
            console.warn('Failed to format timestamp', error);
            element.textContent = isoString;
        }
    }
};



