using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Planora.Api.Application.Interfaces;
using Planora.Api.Infrastructure.Filters;
using Planora.Shared.DTOs.Board;
using Planora.Shared.DTOs.Card;

namespace Planora.Tests.Storage;

/// <summary>
/// The <see cref="MediaUrlResolutionFilter"/> is the single point that signs stored file references
/// into read URLs, so it must reach every URL-bearing shape the API returns (single DTO, list, and
/// attachments nested in a card) and leave everything else alone. A stub <see cref="IFileStorage"/>
/// stands in for real SAS signing.
/// </summary>
public class MediaUrlResolutionFilterTests
{
    // Signs by prefixing, and models the "null cover stays null" contract of the real backends.
    private sealed class StubStorage : IFileStorage
    {
        public Task<string> SaveAsync(Stream c, string d, string e, CancellationToken ct = default) => Task.FromResult("");
        public Task DeleteAsync(string? u, CancellationToken ct = default) => Task.CompletedTask;
        public string? GetReadUrl(string? storedUrl) => storedUrl is null ? null : $"signed::{storedUrl}";
    }

    private static void Run(IActionResult result)
    {
        var filter = new MediaUrlResolutionFilter(new StubStorage());
        var ctx = new ResultExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            [], result, controller: null!);
        filter.OnResultExecuting(ctx);
    }

    [Fact]
    public void Signs_a_board_cover_url()
    {
        var board = new BoardDto { CoverImageUrl = "/uploads/boards/x.png" };
        Run(new OkObjectResult(board));
        Assert.Equal("signed::/uploads/boards/x.png", board.CoverImageUrl);
    }

    [Fact]
    public void Leaves_a_null_cover_null()
    {
        var board = new BoardDto { CoverImageUrl = null };
        Run(new OkObjectResult(board));
        Assert.Null(board.CoverImageUrl);
    }

    [Fact]
    public void Signs_every_board_in_a_list()
    {
        var boards = new List<BoardDto>
        {
            new() { CoverImageUrl = "a.png" },
            new() { CoverImageUrl = "b.png" },
        };
        Run(new OkObjectResult(boards));
        Assert.Equal("signed::a.png", boards[0].CoverImageUrl);
        Assert.Equal("signed::b.png", boards[1].CoverImageUrl);
    }

    [Fact]
    public void Signs_attachments_nested_in_a_card()
    {
        var card = new CardDto { Attachments = [new CardAttachmentDto { Url = "doc.pdf" }] };
        Run(new OkObjectResult(card));
        Assert.Equal("signed::doc.pdf", card.Attachments[0].Url);
    }

    [Fact]
    public void Signs_a_standalone_attachment_dto()
    {
        var attachment = new CardAttachmentDto { Url = "img.png" };
        Run(new OkObjectResult(attachment));
        Assert.Equal("signed::img.png", attachment.Url);
    }

    [Fact]
    public void Ignores_results_without_media()
    {
        // A plain string body must not be walked as a char sequence or otherwise touched.
        var result = new OkObjectResult("just a message");
        var ex = Record.Exception(() => Run(result));
        Assert.Null(ex);
        Assert.Equal("just a message", ((OkObjectResult)result).Value);
    }
}
