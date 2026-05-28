window.docview = {
    setDark: function (isDark) {
        document.documentElement.setAttribute('data-dark', isDark ? 'true' : 'false');
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
    }
};
