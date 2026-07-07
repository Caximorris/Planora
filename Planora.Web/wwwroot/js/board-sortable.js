// SortableJS interop for Board.razor — gives the kanban board real "follow the mouse" drag
// animation for both column reordering and card reordering (including across columns).
// Plain HTML5 drag-and-drop (the previous approach) only fires on dragstart/dragover/drop and
// can't animate intermediate positions live; Sortable.js owns the DOM during the drag and we
// only get told the final result via onEnd, which we then persist through Blazor.

window.planoraInitColumnsSortable = function (dotnetRef) {
    var el = document.querySelector('.board-columns');
    if (!el || el.dataset.sortableInit === 'true') return;
    el.dataset.sortableInit = 'true';

    new Sortable(el, {
        animation: 150,
        handle: '.kanban-col-header',
        draggable: '.kanban-column',
        // Without this, Sortable uses the browser's native HTML5 drag image (which follows the
        // cursor) *on top of* its own ".sortable-ghost" placeholder (left at the drop slot) —
        // two semi-transparent copies on screen at once, looking like a duplicate. Forcing the
        // fallback makes Sortable draw a single clone it fully controls instead.
        forceFallback: true,
        fallbackOnBody: true,
        onEnd: function (evt) {
            var columnId = evt.item.dataset.columnId;
            if (columnId) dotnetRef.invokeMethodAsync('OnColumnsReordered', columnId, evt.newIndex);
        }
    });
};

window.planoraInitCardLists = function (dotnetRef) {
    document.querySelectorAll('.kanban-cards-list').forEach(function (el) {
        var disabled = el.dataset.dndDisabled === 'true';
        if (el._planoraSortable) {
            el._planoraSortable.option('disabled', disabled);
            return;
        }
        if (el.dataset.sortableInit === 'true') return;
        el.dataset.sortableInit = 'true';

        el._planoraSortable = new Sortable(el, {
            group: 'planora-cards',
            animation: 150,
            forceFallback: true,
            fallbackOnBody: true,
            disabled: disabled,
            onEnd: function (evt) {
                var cardId = evt.item.dataset.cardId;
                var targetColumnId = evt.to.dataset.columnId;
                if (cardId && targetColumnId) dotnetRef.invokeMethodAsync('OnCardsReordered', cardId, targetColumnId, evt.newIndex);
            }
        });
    });
};
