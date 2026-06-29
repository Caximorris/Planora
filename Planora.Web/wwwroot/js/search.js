window.planoraSearch = (function () {
    var _handler = null;
    return {
        init: function (dotnetRef) {
            _handler = function (e) {
                if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                    e.preventDefault();
                    dotnetRef.invokeMethodAsync('OpenSearch');
                }
            };
            document.addEventListener('keydown', _handler);
        },
        dispose: function () {
            if (_handler) {
                document.removeEventListener('keydown', _handler);
                _handler = null;
            }
        },
        focusInput: function (el) {
            if (el) { el.focus(); el.select(); }
        }
    };
})();
