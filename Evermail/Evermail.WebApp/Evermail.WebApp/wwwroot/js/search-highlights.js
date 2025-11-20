(function () {
    const states = new Map();

    function clearHighlights(container) {
        container.querySelectorAll('mark.search-hit').forEach(mark => {
            const textNode = document.createTextNode(mark.textContent ?? '');
            mark.replaceWith(textNode);
        });
    }

    function buildRegex(terms) {
        if (!Array.isArray(terms)) {
            return null;
        }

        const escaped = terms
            .filter(term => typeof term === 'string' && term.trim().length > 0)
            .map(term => term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));

        if (!escaped.length) {
            return null;
        }

        return new RegExp(escaped.join('|'), 'gi');
    }

    function highlightNode(node, regex) {
        const text = node.textContent ?? '';
        regex.lastIndex = 0;
        let match;
        let lastIndex = 0;
        const fragment = document.createDocumentFragment();

        while ((match = regex.exec(text)) !== null) {
            if (match.index > lastIndex) {
                fragment.appendChild(document.createTextNode(text.slice(lastIndex, match.index)));
            }

            const mark = document.createElement('mark');
            mark.className = 'search-hit';
            mark.textContent = match[0];
            fragment.appendChild(mark);

            lastIndex = match.index + match[0].length;
        }

        if (lastIndex < text.length) {
            fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
        }

        node.replaceWith(fragment);
    }

    function cacheState(key, container) {
        const hits = Array.from(container.querySelectorAll('mark.search-hit'));
        states.set(key, {
            marks: hits,
            index: 0
        });
        return hits.length;
    }

    function resolveContainer(selectorOrElement) {
        if (typeof selectorOrElement === 'string') {
            return document.querySelector(selectorOrElement);
        }
        return selectorOrElement;
    }

    window.EvermailSearchHighlights = {
        highlightBody: function (selectorOrElement, terms, autoScroll) {
            const container = resolveContainer(selectorOrElement);
            if (!container) {
                return 0;
            }

            clearHighlights(container);
            const regex = buildRegex(terms);
            if (!regex) {
                states.delete(selectorOrElement);
                return 0;
            }

            const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT, null);
            const textNodes = [];
            let current = walker.nextNode();
            while (current) {
                if (!current.parentElement || !current.parentElement.closest('mark.search-hit')) {
                    textNodes.push(current);
                }
                current = walker.nextNode();
            }

            textNodes.forEach(node => highlightNode(node, regex));
            const count = cacheState(selectorOrElement, container);

            if (autoScroll && count > 0) {
                const state = states.get(selectorOrElement);
                state?.marks[0]?.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }

            return count;
        },
        scrollToMatch: function (selectorOrElement, direction) {
            const state = states.get(selectorOrElement);
            if (!state || !state.marks.length) {
                return 0;
            }

            if (direction === 'prev') {
                state.index = (state.index - 1 + state.marks.length) % state.marks.length;
            } else {
                state.index = (state.index + 1) % state.marks.length;
            }

            state.marks[state.index].scrollIntoView({ behavior: 'smooth', block: 'center' });
            return state.index + 1;
        }
    };
})();

