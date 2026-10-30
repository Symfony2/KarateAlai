using System.ComponentModel.DataAnnotations;
using KarateClassLibrary;

namespace KarateTournament.Api.Contracts;

public sealed record CreateParticipantRequest(
    [property: Required(ErrorMessage = "Имя обязательно.")] string? FirstName,
    [property: Required(ErrorMessage = "Фамилия обязательна.")] string? LastName,
    [property: Required(ErrorMessage = "Возраст обязателен."), Range(0, int.MaxValue, ErrorMessage = "Возраст должен быть неотрицательным.")] int? Age,
    string? Club,
    [property: Required(ErrorMessage = "Пояс обязателен."), EnumDataType(typeof(Belts), ErrorMessage = "Недопустимое значение пояса.")] Belts? Belt,
    [property: Required(ErrorMessage = "Пол обязателен."), EnumDataType(typeof(Sex), ErrorMessage = "Недопустимое значение пола.")] Sex? Sex,
    [property: Required(ErrorMessage = "Список дисциплин обязателен."), MinLength(1, ErrorMessage = "Укажите хотя бы одну дисциплину.")] CategoryType[]? CategoryTypes) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CategoryTypes is not null)
        {
            if (CategoryTypes.Any(type => !Enum.IsDefined(type)))
                yield return new ValidationResult(
                    "Список дисциплин содержит недопустимые значения.", [nameof(CategoryTypes)]);

            if (CategoryTypes.Distinct().Count() != CategoryTypes.Length)
                yield return new ValidationResult(
                    "Список дисциплин не должен содержать повторов.", [nameof(CategoryTypes)]);
        }
    }
}
