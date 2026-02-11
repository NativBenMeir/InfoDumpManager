using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using InfoDumpManager.Application.Common.Behaviors;
using MediatR;
using Xunit;

namespace InfoDumpManager.Tests.Unit.Common;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithNoValidators_CallsNext()
    {
        var validators = Enumerable.Empty<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, TestResult>(validators);
        var command = new TestCommand();

        var result = await behavior.Handle(
            command,
            _ => Task.FromResult(new TestResult()),
            CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_WithFailingValidator_ThrowsValidationException()
    {
        var behavior = new ValidationBehavior<TestCommand, TestResult>(new IValidator<TestCommand>[]
        {
            new FailingValidator()
        });

        await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new TestCommand(), _ => Task.FromResult(new TestResult()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithPassingValidator_CallsNext()
    {
        var behavior = new ValidationBehavior<TestCommand, TestResult>(new IValidator<TestCommand>[]
        {
            new PassingValidator()
        });

        var result = await behavior.Handle(
            new TestCommand { Title = "Valid" },
            _ => Task.FromResult(new TestResult()),
            CancellationToken.None);

        Assert.NotNull(result);
    }

    private sealed record TestCommand : IRequest<TestResult>
    {
        public string? Title { get; init; }
    }

    private sealed record TestResult;

    private sealed class PassingValidator : AbstractValidator<TestCommand>
    {
    }

    private sealed class FailingValidator : AbstractValidator<TestCommand>
    {
        public FailingValidator()
        {
            RuleFor(x => x.Title).NotEmpty();
        }
    }
}
