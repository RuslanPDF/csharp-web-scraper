using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
        
        var inputPath = "beauty.jsonl";
        var outputPath = "output.json";
        var objects = new List<JsonElement>();

        int lineNumber = 0;

        foreach (var line in File.ReadLines(inputPath))
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            try
            {
                var obj = JsonSerializer.Deserialize<JsonElement>(trimmed);
                objects.Add(obj);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Ошибка парсинга, строка {lineNumber}: \"{trimmed}\"");
                Console.WriteLine($"Причина: {ex.Message}");
            }
        }

        var prettyJson = JsonSerializer.Serialize(objects, options);
        File.WriteAllText(outputPath, prettyJson);
        Console.WriteLine($"Исправленный JSON сохранён в файл {outputPath}");
    }
}