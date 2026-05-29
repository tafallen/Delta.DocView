window.docview = {
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
    saveTextFile: function (filename, text) {
        try {
            const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
            return true;
        } catch (e) {
            console.warn('docview.saveTextFile failed', e);
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
    focusTrap: {
        _container: null,
        _handler: null,
        activate: function (containerId) {
            if (this._handler) return; // idempotent — already trapping
            const el = document.getElementById(containerId);
            if (!el) return;
            this._container = el;
            window.docview.focus._stack.push(document.activeElement);
            const focusables = function () {
                return el.querySelectorAll(
                    'a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])');
            };
            const first = focusables()[0];
            if (first) first.focus();
            this._handler = function (e) {
                if (e.key !== 'Tab') return;
                const items = focusables();
                if (items.length === 0) return;
                const firstEl = items[0];
                const lastEl = items[items.length - 1];
                if (e.shiftKey && document.activeElement === firstEl) {
                    e.preventDefault();
                    lastEl.focus();
                } else if (!e.shiftKey && document.activeElement === lastEl) {
                    e.preventDefault();
                    firstEl.focus();
                }
            };
            el.addEventListener('keydown', this._handler);
        },
        deactivate: function () {
            if (this._container && this._handler) {
                this._container.removeEventListener('keydown', this._handler);
            }
            this._handler = null;
            this._container = null;
            window.docview.focus.restorePrevious();
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

                // NOTE: This key map is the source of truth for keyboard HANDLING.
                // The shortcuts OVERLAY display list lives in ShortcutDefinitions.cs —
                // if you add/change/remove a binding here, update that file too.
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
    },
    // Storage key 'docview.tweaks.v1' is the contract with TweaksStore.cs.
    // Payload: JSON { dark, accent, density, rowEmphasis, source }. Bump key suffix on schema change.
    tweaks: {
        read: function () {
            try { return window.localStorage.getItem('docview.tweaks.v1') || ''; }
            catch (e) { console.warn('docview.tweaks.read failed', e); return ''; }
        },
        write: function (json) {
            try { window.localStorage.setItem('docview.tweaks.v1', json); }
            catch (e) { console.warn('docview.tweaks.write failed', e); }
        },
        applyRoot: function (dark, accent, density, rowEmphasis) {
            var r = document.documentElement;
            r.setAttribute('data-dark', dark ? 'true' : 'false');
            r.setAttribute('data-accent', accent || 'orange');
            r.setAttribute('data-density', density || 'comfortable');
            r.setAttribute('data-row-emphasis', rowEmphasis || 'pattern');
        },
        _mql: null,
        _mqlListener: null,
        watchOs: function (dotNetRef) {
            try {
                if (this._mqlListener) return; // idempotent — already watching
                this._mql = window.matchMedia('(prefers-color-scheme: dark)');
                this._mqlListener = function (e) {
                    dotNetRef.invokeMethodAsync('OnOsColorSchemeChanged', e.matches);
                };
                this._mql.addEventListener('change', this._mqlListener);
            } catch (e) { console.warn('docview.tweaks.watchOs failed', e); }
        },
        unwatchOs: function () {
            try {
                if (this._mql && this._mqlListener) {
                    this._mql.removeEventListener('change', this._mqlListener);
                }
                this._mql = null;
                this._mqlListener = null;
            } catch (e) { console.warn('docview.tweaks.unwatchOs failed', e); }
        }
    }
};
