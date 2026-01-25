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

public class RemoveGemFromCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_RemovesGemFromCategory()
    {
        var category = Category.Create("Research");
        var gemId = Guid.NewGuid();
        category.AssignGem(gemId);

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

        var handler = new RemoveGemFromCategoryCommandHandler(repository.Object, unitOfWork.Object);
        var result = await handler.Handle(new RemoveGemFromCategoryCommand(category.Id, gemId), CancellationToken.None);

        result.Should().BeTrue();
        category.GemIds.Should().BeEmpty();
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
        var handler = new RemoveGemFromCategoryCommandHandler(repository.Object, unitOfWork.Object);

        var result = await handler.Handle(new RemoveGemFromCategoryCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Should().BeFalse();
        repository.Verify(x => x.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
