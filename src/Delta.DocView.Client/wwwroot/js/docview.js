window.docview = {
    setDark: function (isDark) {
        document.documentElement.setAttribute('data-dark', isDark ? 'true' : 'false');
    },
    prefersDark: function () {
        try {
            return window.matchMedia('(prefers-color-scheme: dark)').matches;
        } catch (e) {
            return false;
        }
    },
    setDomainPalette: function (css) {
        let el = document.getElementById('dom-palette');
        if (!el) {
            el = document.createElement('style');
            el.id = 'dom-palette';
            document.head.appendChild(el);
        }
        el.textContent = css;
    },
    // Storage key 'docview.favs.v1' is the contract with LocalStorageFavouritesStore.cs.
    // Payload: JSON string[] of step ids, ordinal-sorted on write. Bump key suffix on schema change.
    favourites: {
        read: function () {
            try {
                const raw = window.localStorage.getItem('docview.favs.v1');
                return raw === null ? '[]' : raw;
            } catch (e) {
                console.warn('docview.favourites.read failed', e);
                return '[]';
            }
        },
        write: function (json) {
            try {
                window.localStorage.setItem('docview.favs.v1', json);
            } catch (e) {
                console.warn('docview.favourites.write failed', e);
            }
        }
    },
    copyText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (e) {
            console.warn('docview.copyText failed', e);
            return false;
        }
    },
    platform: {
        isMac: function () {
            try {
                const p = navigator.platform || '';
                const ua = navigator.userAgent || '';
                return /Mac/i.test(p) || /Mac/i.test(ua);
            } catch (e) {
                return false;
            }
        }
    },
    focus: {
        _stack: [],
        element: function (id) {
            try {
                this._stack.push(document.activeElement);
                const el = document.getElementById(id);
                if (el) el.focus();
            } catch (e) { /* swallow */ }
        },
        restorePrevious: function () {
            try {
                const prev = this._stack.pop();
                if (prev && typeof prev.focus === 'function') {
                    prev.focus();
                }
            } catch (e) { /* swallow */ }
        }
    },
    scrollIntoViewIfNeeded: function (selector) {
        try {
            const el = document.querySelector(selector);
            if (el && typeof el.scrollIntoView === 'function') {
                el.scrollIntoView({ block: 'nearest' });
            }
        } catch (e) { /* swallow */ }
    },
    keyboard: {
        _ref: null,
        _handler: null,

        attach: function (dotnetRef) {
            if (this._handler) return; // idempotent — already attached
            this._ref = dotnetRef;
            const self = this;
            this._handler = function (e) {
                // Skip when the user is typing in an input / textarea / contenteditable
                const a = document.activeElement;
                if (a && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA' || a.isContentEditable)) {
                    return;
                }

                const key = e.key;
                const lower = key.length === 1 ? key.toLowerCase() : key;
                let action = null;

                if ((e.ctrlKey || e.metaKey) && lower === 'k') action = 'open-palette';
                else if (key === '/') action = 'open-palette';
                else if (key === '?') action = 'open-shortcuts';
                else if (lower === 'c' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'toggle-composer';
                else if (lower === 'f' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'toggle-fav';
                else if (lower === 'j' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'select-next';
                else if (lower === 'k' && !e.ctrlKey && !e.metaKey && !e.altKey) action = 'select-prev';
                else if (key === 'Escape') action = 'close-overlay';

                if (action) {
                    e.preventDefault();
                    self._ref.invokeMethodAsync('OnKey', action);
                }
            };
            window.addEventListener('keydown', this._handler);
        },

        detach: function () {
            if (this._handler) {
                window.removeEventListener('keydown', this._handler);
                this._handler = null;
                this._ref = null;
            }
        }
    }
};
