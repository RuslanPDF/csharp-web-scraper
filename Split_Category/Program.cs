using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    static void Main()
    {
        string resultsFolder = "results";       
        string resultsJsonl = "results.jsonl"; 
        string outputFolder = "output";        

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);
        
        var urlToObject = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(resultsJsonl))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JObject obj = JObject.Parse(line);
            string url = obj["Url"]?.ToString();
            if (!string.IsNullOrWhiteSpace(url))
            {
                urlToObject[url] = obj;
            }
        }

        foreach (var filePath in Directory.GetFiles(resultsFolder, "*.json"))
        {
            string fileName = Path.GetFileName(filePath);

            var urlsInFile = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(filePath));

            if (urlsInFile == null || urlsInFile.Count == 0)
                continue;

            JArray matchedObjects = new JArray();

            foreach (var url in urlsInFile)
            {
                if (urlToObject.TryGetValue(url, out var obj))
                {
                    matchedObjects.Add(obj);
                }
            }

            if (matchedObjects.Count > 0)
            {
                string outputPath = Path.Combine(outputFolder, fileName);
                File.WriteAllText(outputPath, matchedObjects.ToString(Formatting.Indented));
                Console.WriteLine($"Сохранено {matchedObjects.Count} объектов в {outputPath}");
            }
        }
    }
}
