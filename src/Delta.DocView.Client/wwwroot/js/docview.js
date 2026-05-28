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
    }
};
