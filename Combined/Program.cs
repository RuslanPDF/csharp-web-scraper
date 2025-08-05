using System.Text.Json;
using System.Text.Json.Nodes;

class Program
{
    static async Task Main()
    {
        string folderName = "trade";
        string folderPath = Path.Combine("results", folderName);

        var uniqueItems = new Dictionary<string, JsonNode>();

        Console.WriteLine("Читаем файлы из: " + Path.GetFullPath(folderPath));

        foreach (var filePath in Directory.GetFiles(folderPath, "*.json"))
        {
            try
            {
                string jsonContent = await File.ReadAllTextAsync(filePath);
                var jsonArray = JsonNode.Parse(jsonContent)?.AsArray();

                if (jsonArray == null)
                {
                    Console.WriteLine($"Файл {filePath} не содержит JSON-массив");
                    continue;
                }

                foreach (var element in jsonArray)
                {
                    var urlNode = element?["Url"];
                    if (urlNode != null)
                    {
                        string url = urlNode.ToString();
                        if (!uniqueItems.ContainsKey(url))
                        {
                            uniqueItems[url] = element;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"В файле {filePath} найден элемент без поля Url");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке файла {filePath}: {ex.Message}");
            }
        }

        Console.WriteLine($"Уникальных записей всего: {uniqueItems.Count}");

        string outputFile = $"{folderName}.json";

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        await File.WriteAllTextAsync(outputFile, JsonSerializer.Serialize(uniqueItems.Values, options));
        Console.WriteLine($"Объединённый файл сохранён: {outputFile}");
    }
}
