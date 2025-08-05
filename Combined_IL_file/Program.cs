using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    static void Main()
    {
        string name = "valid_description_about";
        string inputFile1 = $"services/{name}.json";
        string inputFile2 = $"trade/{name}.json";

        try
        {
            var json1 = JArray.Parse(File.ReadAllText(inputFile1));
            var json2 = JArray.Parse(File.ReadAllText(inputFile2));

            var combined = new JArray();
            combined.Merge(json1);
            combined.Merge(json2);

            File.WriteAllText($"{name}.json", combined.ToString(Formatting.Indented));
            Console.WriteLine($"Файлы успешно объединены в {name}.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при объединении файлов: {ex.Message}");
        }
    }
}