using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using InfoDumpManager.Application.GEMs.Commands;
using InfoDumpManager.Application.GEMs.Commands.Handlers;
using InfoDumpManager.Application.GEMs.Dtos;
using InfoDumpManager.Application.Services;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using InfoDumpManager.Domain.ValueObjects;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.GEMs.Commands;

public sealed class CreateGemCommandHandlerTests
{
    [Fact]
    public async Task Handle_CapturesSnapshotAndStoresIt()
    {
        var pageSnapshot = new PageSnapshot("<html>body</html>", "text/html", DateTime.UtcNow);
        var snapshotMock = new Mock<IPageSnapshotService>();
        snapshotMock
            .Setup(s => s.CaptureAsync("https://example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pageSnapshot);

        var repositoryMock = new Mock<IGEMRepository>();
        GEM? savedGem = null;
        repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<GEM>(), It.IsAny<CancellationToken>()))
            .Callback<GEM, CancellationToken>((gem, _) => savedGem = gem)
            .Returns(Task.CompletedTask);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var mapperMock = new Mock<IMapper>();
        mapperMock
            .Setup(m => m.Map<GemDto>(It.IsAny<GEM>()))
            .Returns<GEM>(gem => new GemDto
            {
                Id = gem.Id,
                Title = gem.Title,
                SourceUrl = gem.Source.Url,
                CreatedAtUtc = gem.CreatedAtUtc,
                UpdatedAtUtc = gem.UpdatedAtUtc,
                CategoryIds = gem.CategoryIds.ToArray(),
                SnapshotContent = gem.Snapshot?.Content,
                SnapshotContentType = gem.Snapshot?.ContentType,
                SummaryText = gem.Summary?.Text
            });

        var storedName = string.Empty;
        var storedContent = string.Empty;
        var storedContentType = string.Empty;
        var storageMock = new Mock<ISnapshotStorageService>();
        storageMock
            .Setup(s => s.StoreSnapshotAsync(It.IsAny<string>(), It.IsAny<Stream>(), pageSnapshot.ContentType, It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((name, stream, contentType, _) =>
            {
                storedName = name;
                storedContentType = contentType;
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                storedContent = reader.ReadToEnd();
            })
            .ReturnsAsync(new Uri("https://storage.local/snapshot"));

        var handler = new CreateGemCommandHandler(
            repositoryMock.Object,
            unitOfWorkMock.Object,
            mapperMock.Object,
            snapshotMock.Object,
            storageMock.Object);

        var request = new CreateGemCommand("https://example.com", "Test Title");

        var result = await handler.Handle(request, CancellationToken.None);

        repositoryMock.Verify(r => r.AddAsync(It.IsAny<GEM>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        snapshotMock.Verify(s => s.CaptureAsync(request.Url, It.IsAny<CancellationToken>()), Times.Once);
        storageMock.Verify(s => s.StoreSnapshotAsync(It.Is<string>(name => name.StartsWith($"gems/{result.Id}-") && name.EndsWith(".html")), It.IsAny<Stream>(), pageSnapshot.ContentType, It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(savedGem);
        Assert.NotNull(savedGem!.Snapshot);
        Assert.Equal(pageSnapshot.Content, savedGem.Snapshot.Content);
        Assert.Equal(pageSnapshot.ContentType, savedGem.Snapshot.ContentType);
        Assert.Equal(pageSnapshot.Content, storedContent);
        Assert.Equal(pageSnapshot.ContentType, storedContentType);
        Assert.Contains(result.Id.ToString(), storedName);
        Assert.Equal(result.Id, savedGem.Id);
        Assert.Equal(pageSnapshot.Content, result.SnapshotContent);
        Assert.Equal(pageSnapshot.ContentType, result.SnapshotContentType);
    }
}
