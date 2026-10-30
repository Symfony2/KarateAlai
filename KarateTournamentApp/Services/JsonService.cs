using KarateTournamentApp.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KarateTournamentApp.Services
{
    public class JsonService
    {
         private JsonSerializerOptions GetOptions()
         {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                // JudgingScores uses ValueTuple, whose components are public fields.
                IncludeFields = true,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
         }

        /// <summary>
        /// Asynchronously saves tournament data to JSON file
        /// </summary>
        public async Task SaveTournamentDataAsync(string filePath, List<Category> categories)
        {
            string jsonString = JsonSerializer.Serialize(categories, GetOptions());
            await File.WriteAllTextAsync(filePath, jsonString);
        }

        /// <summary>
        /// Asynchronously loads tournament data from JSON file
        /// </summary>
        public async Task<List<Category>> LoadTournamentDataAsync(string filePath)
        {
            if (!File.Exists(filePath)) 
                return new List<Category>();

            string jsonString = await File.ReadAllTextAsync(filePath);
            var categories = JsonSerializer.Deserialize<List<Category>>(jsonString, GetOptions())
                ?? throw new InvalidDataException("Файл JSON турнира не содержит списка категорий.");
            TournamentIdentityNormalizer.Normalize(categories);
            return categories;
        }
    }
}
