using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
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

        email = Regex.Replace(email, "<.*?>", string.Empty);

        email = email.Trim();

        var emailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");

        return emailRegex.IsMatch(email);
    }

    static void Main()
    {
        string name = "valid_description_about";
        string inputPath = $"{name}.json";

        var json = File.ReadAllText(inputPath);
        var items = JsonConvert.DeserializeObject<List<SiteInfo>>(json);

        int parts = 3;
        int chunkSize = (int)Math.Ceiling(items.Count / (double)parts);

        for (int part = 0; part < parts; part++)
        {
            var chunkItems = items.Skip(part * chunkSize).Take(chunkSize).ToList();

            var csv = new StringBuilder();
            csv.AppendLine("Tel,Email,Title,Url,Desciption,About,Vk,Instagram,Telegram,Whatsapp");

            foreach (var item in chunkItems)
            {
                var validEmails = (item.Email ?? new List<string>()).Where(e => IsValidEmail(e)).ToList();
                var invalidEmails = (item.Email ?? new List<string>()).Where(e => !IsValidEmail(e)).ToList();

                string emailsStr = string.Join(";", validEmails);
                if (invalidEmails.Any())
                {
                    emailsStr += " [Invalid: " + string.Join(";", invalidEmails) + "]";
                }

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

            string outputPath = $"{name}_{part + 1}.csv";
            File.WriteAllText(outputPath, csv.ToString(), Encoding.UTF8);
            Console.WriteLine($"Готово! CSV сохранён в {outputPath}");
        }
    }
}
