namespace Planora.Shared.Constants;

public static class CardLimits
{
    /// Shared cap for card attachments, enforced by the API and by the Blazor upload stream.
    public const long MaxAttachmentBytes = 10 * 1024 * 1024;
}
