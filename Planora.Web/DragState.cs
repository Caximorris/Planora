namespace Planora.Web;

// Column and card drag-and-drop are handled by SortableJS (see wwwroot/js/board-sortable.js +
// Board.razor's JSInvokable handlers) — only the plain board-tile reordering in Workspaces.razor
// still uses native HTML5 drag-and-drop with this shared state.
public static class DragState
{
    public static Guid? DraggedBoardId { get; set; }
}
