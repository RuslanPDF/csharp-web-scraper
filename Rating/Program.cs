using System.Text.Encodings.Web;
using System.Text.Json;
using HtmlAgilityPack;

class Program
{
    static async Task Main(string[] args)
    {
        List<string> baseUrls = new List<string> {
            "https://wwwrating.com/top/retail/marketplace/",
            "https://wwwrating.com/top/retail/appliances/",
            "https://wwwrating.com/top/retail/drogerie/",
            "https://wwwrating.com/top/retail/pet/",
            "https://wwwrating.com/top/retail/stationery/",
            "https://wwwrating.com/top/retail/сeramic/",
            "https://wwwrating.com/top/retail/climate/",
            "https://wwwrating.com/top/retail/books/",
            "https://wwwrating.com/top/retail/сosmetics/",
            "https://wwwrating.com/top/retail/matras/",
            "https://wwwrating.com/top/retail/mebel/",
            "https://wwwrating.com/top/retail/okna/",
            "https://wwwrating.com/top/retail/plumbing/",
            "https://wwwrating.com/top/retail/security/",
            "https://wwwrating.com/top/retail/sport/",
            "https://wwwrating.com/top/retail/remont/",
            "https://wwwrating.com/top/retail/baby/",
            "https://wwwrating.com/top/retail/flowers/",
            "https://wwwrating.com/top/retail/tea/",
            "https://wwwrating.com/top/retail/dveri/",
            
            "https://wwwrating.com/top/auto/avtozapchasti/",
            
            "https://wwwrating.com/top/medicine/clinic/",
            "https://wwwrating.com/top/medicine/stomatology/",
            
            "https://wwwrating.com/top/services/staff/",
            "https://wwwrating.com/top/services/catering/",
            "https://wwwrating.com/top/services/branding/",
            "https://wwwrating.com/top/services/audit/",
            
            "https://wwwrating.com/top/construction/district/",
            "https://wwwrating.com/top/construction/developer/",
            "https://wwwrating.com/top/construction/catalog-buildings/",
            "https://wwwrating.com/top/construction/low-rise/",
        };
        
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        foreach (var baseUrl in baseUrls)
        {
            var page = 1;
            bool hasNextPage = true;
            var allValidLinks = new List<string>();

            while (hasNextPage)
            {
                string url = page == 1 ? baseUrl : $"{baseUrl}/?PAGEN_1={page}";
                var html = await DownloadPageAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var tableRows = doc.DocumentNode.SelectNodes("//table[contains(@class, 'table-stats')]//tr");
                if (tableRows == null) break;

                foreach (var row in tableRows.Skip(1))
                {
                    var linkNode = row.SelectSingleNode(".//a[contains(@href, '/top/')]");
                    if (linkNode != null)
                    {
                        string href = linkNode.GetAttributeValue("href", "");
                        if (IsValidLink(href))
                        {
                            string absoluteUrl = MakeAbsoluteUrl(baseUrl, href);
                            
                            var html2 = await DownloadPageAsync(absoluteUrl);
                            var doc2 = new HtmlDocument();
                            doc2.LoadHtml(html2);

                            var ddNode = doc2.DocumentNode.SelectSingleNode("//dl[contains(@class, 'company-features')]//dd/a");
                            if (ddNode != null)
                            {
                                string href2 = ddNode.GetAttributeValue("href", "");
                                allValidLinks.Add(href2);
                            }
                        }
                    }
                }

                hasNextPage = doc.DocumentNode.SelectSingleNode($"//a[contains(@href, '?PAGEN_1={page + 1}')]") != null;
                page++;
            }
            
            Uri uri = new Uri(baseUrl);
            string[] segments = uri.Segments;

            string lastSegment = segments[segments.Length - 1].Trim('/');

            using (var writer = new StreamWriter($"results/{lastSegment}.json"))
            {
                string json = JsonSerializer.Serialize(allValidLinks.Distinct().ToList(), options);
                await writer.WriteLineAsync(json);
            }
        }
    }

    static async Task<string> DownloadPageAsync(string url)
    {
        using (var client = new HttpClient())
        {
            return await client.GetStringAsync(url);
        }
    }

    static bool IsValidLink(string url)
    {
        return !string.IsNullOrEmpty(url) &&
            !url.Contains("?") && !url.Contains("=") && !url.StartsWith("#");
    }

    static string MakeAbsoluteUrl(string baseUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absUri))
            return href;
        return new Uri(new Uri(baseUrl), href).ToString();
    }
}
