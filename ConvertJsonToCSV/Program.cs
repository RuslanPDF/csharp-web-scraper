using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

public class SiteInfo
{
    public List<string> Tel { get; set; }
    public List<string> Email { get; set; }
    public string Title { get; set; }
    public string Url { get; set; }
    public string Desciption { get; set; }
    public List<string> Vk { get; set; }
    public List<string> Instagram { get; set; }
    public List<string> Telegram { get; set; }
    public List<string> Whatsapp { get; set; }
    public string About { get; set; }
}

class Program
{
    static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        if (email.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            email = email.Substring(7);
        email = Regex.Replace(email, "<.*?>", string.Empty).Trim();
        var emailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
        return emailRegex.IsMatch(email);
    }

    static List<List<T>> ChunkList<T>(List<T> source, int chunkSize)
    {
        var chunks = new List<List<T>>();
        for (int i = 0; i < source.Count; i += chunkSize)
            chunks.Add(source.GetRange(i, Math.Min(chunkSize, source.Count - i)));
        return chunks;
    }

    static void Main()
    {
        string name = "valid_description_about";
        string folderName = "оптовик";
        string inputPath = $"{folderName}/{name}.json";

        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"Файл не найден: {inputPath}");
            return;
        }

        var json = File.ReadAllText(inputPath);
        var items = JsonConvert.DeserializeObject<List<SiteInfo>>(json);

        var expandedItems = new List<SiteInfo>();
        foreach (var item in items)
        {
            if (item.Email != null && item.Email.Count > 0)
            {
                var validEmails = item.Email.Where(e => IsValidEmail(e)).ToList();
                if (validEmails.Count == 0)
                    continue;
                foreach (var singleEmail in validEmails)
                {
                    expandedItems.Add(new SiteInfo
                    {
                        Tel = item.Tel,
                        Email = new List<string> { singleEmail },
                        Title = item.Title,
                        Url = item.Url,
                        Desciption = item.Desciption,
                        Vk = item.Vk,
                        Instagram = item.Instagram,
                        Telegram = item.Telegram,
                        Whatsapp = item.Whatsapp,
                        About = item.About
                    });
                }
            }
            else
            {
            }
        }

        string splitMode = "1000";

        List<List<SiteInfo>> splitted;

        switch (splitMode)
        {
            case "3files":
                int parts = 3;
                int chunkSize3files = (int)Math.Ceiling(expandedItems.Count / (double)parts);
                splitted = new List<List<SiteInfo>>();
                for (int part = 0; part < parts; part++)
                {
                    var chunk = expandedItems.Skip(part * chunkSize3files).Take(chunkSize3files).ToList();
                    if (chunk.Count > 0)
                        splitted.Add(chunk);
                }

                break;
            case "1000":
                splitted = ChunkList(expandedItems, 1000);
                break;
            case "400":
                splitted = ChunkList(expandedItems, 400);
                break;
            case "560":
                splitted = ChunkList(expandedItems, 560);
                break;
            default:
                throw new Exception("Unknown split mode");
        }

        for (int i = 0; i < splitted.Count; i++)
        {
            var chunkItems = splitted[i];
            var csv = new StringBuilder();
            csv.AppendLine("Tel,Email,Title,Url,Desciption,About,Vk,Instagram,Telegram,Whatsapp");

            foreach (var item in chunkItems)
            {
                var validEmails = (item.Email ?? new List<string>()).Where(e => IsValidEmail(e)).ToList();
                if (!validEmails.Any())
                    continue;
                string emailsStr = string.Join(";", validEmails);

                csv.AppendLine(
                    $"\"{string.Join(";", item.Tel ?? new List<string>())}\"," +
                    $"\"{emailsStr}\"," +
                    $"\"{item.Title?.Replace("\"", "\"\"")}\"," +
                    $"\"{item.Url?.Replace("\"", "\"\"")}\"," +
                    $"\"{item.Desciption?.Replace("\"", "\"\"")}\"," +
                    $"\"{item.About?.Replace("\"", "\"\"")}\"," +
                    $"\"{string.Join(";", item.Vk ?? new List<string>())}\"," +
                    $"\"{string.Join(";", item.Instagram ?? new List<string>())}\"," +
                    $"\"{string.Join(";", item.Telegram ?? new List<string>())}\"," +
                    $"\"{string.Join(";", item.Whatsapp ?? new List<string>())}\""
                );
            }

            string outputPath = $"{folderName}/{name}_{i + 1}.csv";
            File.WriteAllText(outputPath, csv.ToString(), Encoding.UTF8);
            Console.WriteLine($"Готово! CSV сохранён в {outputPath}");
        }
    }
}
