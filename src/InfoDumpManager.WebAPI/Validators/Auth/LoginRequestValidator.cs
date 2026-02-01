using FluentValidation;
using InfoDumpManager.WebAPI.Contracts.Auth;

namespace InfoDumpManager.WebAPI.Validators.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
