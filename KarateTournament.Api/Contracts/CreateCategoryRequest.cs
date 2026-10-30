using System.ComponentModel.DataAnnotations;
using KarateClassLibrary;

namespace KarateTournament.Api.Contracts;

public sealed record CreateCategoryRequest(
    [property: Required(ErrorMessage = "Название категории обязательно.")] string? Name,
    [property: Range(0, int.MaxValue, ErrorMessage = "Минимальный возраст должен быть неотрицательным.")] int? MinAge,
    [property: Range(0, int.MaxValue, ErrorMessage = "Максимальный возраст должен быть неотрицательным.")] int? MaxAge,
    [property: Required(ErrorMessage = "Пол обязателен."), EnumDataType(typeof(Sex), ErrorMessage = "Недопустимое значение пола.")] Sex? Sex,
    [property: Required(ErrorMessage = "Тип категории обязателен."), EnumDataType(typeof(CategoryType), ErrorMessage = "Недопустимый тип категории.")] CategoryType? CategoryType,
    [property: Required(ErrorMessage = "Список поясов обязателен."), MinLength(1, ErrorMessage = "Укажите хотя бы один пояс.")] Belts[]? AllowedBelts,
    [property: Required(ErrorMessage = "Список спортсменов обязателен.")] int[]? ParticipantIds) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinAge.HasValue && MaxAge.HasValue && MinAge > MaxAge)
            yield return new ValidationResult(
                "Минимальный возраст не может превышать максимальный.", [nameof(MinAge), nameof(MaxAge)]);

        if (AllowedBelts is not null)
        {
            if (AllowedBelts.Any(belt => !Enum.IsDefined(belt)))
                yield return new ValidationResult(
                    "Список поясов содержит недопустимые значения.", [nameof(AllowedBelts)]);
            if (AllowedBelts.Distinct().Count() != AllowedBelts.Length)
                yield return new ValidationResult(
                    "Список поясов не должен содержать повторов.", [nameof(AllowedBelts)]);
        }

        if (ParticipantIds is not null)
        {
            if (ParticipantIds.Any(id => id <= 0))
                yield return new ValidationResult(
                    "Идентификаторы спортсменов должны быть положительными.", [nameof(ParticipantIds)]);
            if (ParticipantIds.Distinct().Count() != ParticipantIds.Length)
                yield return new ValidationResult(
                    "Идентификаторы спортсменов не должны повторяться.", [nameof(ParticipantIds)]);
        }
    }
}
