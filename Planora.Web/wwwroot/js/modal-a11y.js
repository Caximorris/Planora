// Accessibility layer for Planora's hand-rolled modals (Board, KanbanColumn, Workspaces, …).
// Those dialogs render as `.modal.d-block` siblings of a `.modal-backdrop`, with a `.btn-close`
// in the header and (usually) a backdrop click-to-close. This script adds, globally and with
// zero per-modal wiring: body scroll-lock, focus-move-on-open, a Tab focus trap, Escape-to-close,
// and focus restore on close. See docs/MOBILE_AUDIT.md P2-2.
(function () {
    var lockCount = 0;
    var lastFocused = null;

    var FOCUSABLE = 'a[href], button:not([disabled]), textarea:not([disabled]), ' +
        'input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

    function isModal(node) {
        return node.nodeType === 1 && node.classList &&
            node.classList.contains('modal') && node.classList.contains('d-block');
    }

    function focusables(container) {
        return Array.prototype.slice
            .call(container.querySelectorAll(FOCUSABLE))
            .filter(function (el) { return el.offsetParent !== null; });
    }

    function topModal() {
        var open = document.querySelectorAll('.modal.d-block');
        return open.length ? open[open.length - 1] : null;
    }

    function lock() {
        if (lockCount++ === 0) document.body.classList.add('planora-modal-open');
    }

    function unlock() {
        if (lockCount > 0 && --lockCount === 0) document.body.classList.remove('planora-modal-open');
    }

    function onKeydown(e) {
        var modal = topModal();
        if (!modal) return;

        if (e.key === 'Escape') {
            // Prefer the header close button (some backdrops don't close); fall back to backdrop.
            var btn = modal.querySelector('.btn-close:not([disabled])');
            if (btn) { e.preventDefault(); btn.click(); return; }
            var backs = document.querySelectorAll('.modal-backdrop');
            if (backs.length) { e.preventDefault(); backs[backs.length - 1].click(); }
            return;
        }

        if (e.key === 'Tab') {
            var f = focusables(modal);
            if (f.length === 0) return;
            var first = f[0], last = f[f.length - 1];
            if (e.shiftKey && document.activeElement === first) {
                e.preventDefault(); last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault(); first.focus();
            } else if (!modal.contains(document.activeElement)) {
                e.preventDefault(); first.focus();
            }
        }
    }

    function onOpen(node) {
        lastFocused = document.activeElement;
        lock();
        var f = focusables(node);
        if (!f.length) return;
        // Land on the first real control, not the "×" close button, when there is one.
        var target = f.filter(function (el) { return !el.classList.contains('btn-close'); })[0] || f[0];
        // Defer a tick so the element is laid out and focusable.
        setTimeout(function () { try { target.focus(); } catch (e) { } }, 0);
    }

    function onClose() {
        unlock();
        if (lastFocused && lastFocused.focus) {
            try { lastFocused.focus(); } catch (e) { }
        }
    }

    var observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (m) {
            for (var i = 0; i < m.addedNodes.length; i++) {
                if (isModal(m.addedNodes[i])) onOpen(m.addedNodes[i]);
            }
            for (var j = 0; j < m.removedNodes.length; j++) {
                if (isModal(m.removedNodes[j])) onClose();
            }
        });
    });

    function start() {
        observer.observe(document.body, { childList: true, subtree: true });
        document.addEventListener('keydown', onKeydown, true);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
