namespace Planora.Shared.Constants;

public static class BoardLimits
{
    /// Single source of truth for the cover-image size cap, enforced both server-side
    /// (BoardsController) and client-side (BoardService, before the bytes are even sent).
    public const long MaxCoverImageBytes = 5 * 1024 * 1024;
}
