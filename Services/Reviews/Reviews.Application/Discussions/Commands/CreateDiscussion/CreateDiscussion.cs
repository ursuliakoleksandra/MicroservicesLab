using FluentValidation;
using MediatR;
using Reviews.Application.Common.Messaging;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Discussions.Commands.CreateDiscussion;

public record CreateDiscussionCommand(long ProductId, string ProductName, string Title, IReadOnlyList<string>? Tags = null)
    : ICommand<string>;

public class CreateDiscussionCommandHandler(IDiscussionRepository discussions)
    : IRequestHandler<CreateDiscussionCommand, string>
{
    public async Task<string> Handle(CreateDiscussionCommand cmd, CancellationToken ct)
    {
        var discussion = new Discussion(cmd.ProductId, cmd.ProductName, cmd.Title, cmd.Tags);
        await discussions.AddAsync(discussion, ct);
        return discussion.Id.ToString();
    }
}

public class CreateDiscussionCommandValidator : AbstractValidator<CreateDiscussionCommand>
{
    public CreateDiscussionCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Tags).Must(t => t!.Count <= 10).When(x => x.Tags is not null).WithMessage("At most 10 tags");
    }
}
