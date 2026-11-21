using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsList;

namespace Reviews.WebApi.Controllers;

public record UpdateReviewRequest(int Rating, string? Text);
public record AddCommentRequest(string AuthorName, string Text);

/// <summary>Кожна операція — команда або запит MediatR. Некоректні дані відхиляє ValidationBehavior ще до виклику обробника.</summary>
[Route("api/reviews")]
public class ReviewsController : ApiControllerBase
{
    /// <summary>Список відгуків: фільтр за товаром, повнотекстовий пошук (search), мінімальний рейтинг, сортування (newest | oldest | rating), пагінація.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ReviewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetList(
        [FromQuery] long? productId, [FromQuery] string? search, [FromQuery] int? minRating,
        [FromQuery] string sortBy = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetReviewsListQuery(productId, page, pageSize, search, minRating, sortBy), ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    /// <summary>Відгук за Id. У заголовку ETag — версія документа (для If-Match).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(string id, CancellationToken ct)
    {
        var review = await Mediator.Send(new GetReviewByIdQuery(id), ct);
        SetETag(review.Version);
        return Ok(review);
    }

    /// <summary>Створити відгук. 201 + Location із рядковим ObjectId. Повторний відгук автора на товар — 400 (валідатор) або 409 (унікальний індекс).</summary>
    [HttpPost]
    [ProducesResponseType<string>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<string>> Create(CreateReviewCommand command, CancellationToken ct)
    {
        var id = await Mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>Змінити рейтинг/текст. Заголовок If-Match — очікувана версія; застаріла версія — 409.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Update(string id, UpdateReviewRequest request, CancellationToken ct)
    {
        var review = await Mediator.Send(new UpdateReviewCommand(id, request.Rating, request.Text, ExpectedVersion), ct);
        SetETag(review.Version);
        return Ok(review);
    }

    /// <summary>Додати вкладений коментар.</summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> AddComment(string id, AddCommentRequest request, CancellationToken ct)
    {
        var review = await Mediator.Send(new AddCommentCommand(id, request.AuthorName, request.Text, ExpectedVersion), ct);
        SetETag(review.Version);
        return CreatedAtAction(nameof(GetById), new { id }, review);
    }

    /// <summary>Видалити відгук (If-Match — очікувана версія).</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteReviewCommand(id, ExpectedVersion), ct);
        return NoContent();
    }
}
