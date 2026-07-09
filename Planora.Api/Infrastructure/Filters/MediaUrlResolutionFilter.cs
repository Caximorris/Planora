using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Planora.Api.Application.Interfaces;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;

namespace Planora.Api.Infrastructure.Filters;

/// <summary>
/// Single choke point that turns stored file references into browser-fetchable read URLs on the way
/// out. With the private Azure Blob backend that means signing a short-lived SAS URL; with local
/// disk it's a no-op. Applying it here (rather than at each controller return) means no endpoint —
/// board list, card detail, search, calendar, or any future one — can accidentally leak an
/// unsigned, unfetchable blob URL.
/// </summary>
public sealed class MediaUrlResolutionFilter(IFileStorage storage) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: { } value })
            Resolve(value);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    private void Resolve(object value)
    {
        switch (value)
        {
            case BoardDto board:
                board.CoverImageUrl = storage.GetReadUrl(board.CoverImageUrl);
                break;
            case CardDto card:
                foreach (var attachment in card.Attachments)
                    attachment.Url = storage.GetReadUrl(attachment.Url) ?? attachment.Url;
                break;
            case CardAttachmentDto attachment:
                attachment.Url = storage.GetReadUrl(attachment.Url) ?? attachment.Url;
                break;
            // Lists of the above (board grid, calendar cards, …). String is IEnumerable too — the
            // typed cases above run first, so only collections reach here.
            case IEnumerable sequence:
                foreach (var item in sequence)
                    if (item is not null) Resolve(item);
                break;
        }
    }
}
