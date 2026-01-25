using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using InfoDumpManager.Application.Categories.Commands;
using InfoDumpManager.Application.Categories.Commands.Handlers;
using InfoDumpManager.Domain.Common;
using InfoDumpManager.Domain.Entities;
using InfoDumpManager.Domain.Repositories;
using Moq;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Application.Categories.Commands;

public class AssignGemToCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_AssignsGemToCategory()
    {
        var category = Category.Create("News");
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetByIdAsync(category.Id, CancellationToken.None))
            .ReturnsAsync(category);
        repository
            .Setup(x => x.UpdateAsync(category, CancellationToken.None))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.SaveChangesAsync(CancellationToken.None))
            .ReturnsAsync(1);

        var handler = new AssignGemToCategoryCommandHandler(repository.Object, unitOfWork.Object);
        var gemId = Guid.NewGuid();
        var result = await handler.Handle(new AssignGemToCategoryCommand(category.Id, gemId), CancellationToken.None);

        result.Should().BeTrue();
        category.GemIds.Should().Contain(gemId);
        repository.Verify(x => x.UpdateAsync(category, CancellationToken.None), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ReturnsFalse()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None))
            .ReturnsAsync((Category?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new AssignGemToCategoryCommandHandler(repository.Object, unitOfWork.Object);

        var result = await handler.Handle(new AssignGemToCategoryCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Should().BeFalse();
        repository.Verify(x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
