using System.Text.Json;
using Microsoft.Playwright;
using JsonSerializer = System.Text.Json.JsonSerializer;

class Program
{
    private static readonly string vpsProxyServer = "http://admin:admin@185.117.119.166:3128";

    private static readonly string[] aboutTexts =
    {
        "О компании", "О нас", "О магазине", "О фирме", "О проекте", "О сервисе", "О платформе", "О бренде",
        "О производителе", "О себе",
        "About", "About us", "About company", "About store", "About shop", "About project"
    };
    
    private static readonly string[] errorTitles = {
        "502 Bad Gateway",
        "407 Proxy Authentication Required",
        "403 Forbidden",
        "404 Not Found",
        "400 Bad Request",
        "500 Internal Server Error",
        "503 Service Unavailable",
        "504 Gateway Timeout",
        "502 Proxy Error",
        "Error",
        "Access Denied",
        "Service Unavailable",
        "Bad Gateway",
        "Gateway Timeout",
        "Unauthorized",
        "Proxy Authentication Required",
        "Not Found",
        "Request Timeout",
        "Internal Server Error",
        "Service Temporarily Unavailable",
        "Request Entity Too Large",
        "Too Many Requests",
        "Connection Timed Out",
        "DNS error",
        "Site is down",
        "Forbidden",
        "Unable to connect",
        "Temporary Error",
        "Error 502",
        "Error 503",
        "Error 504",
        "Bad Request",
        "Access Denied",
        "Page Not Found",
        "Problem loading page",
        "Unavailable",
        "Unknown host",
        "DNS_PROBE_FINISHED_NXDOMAIN"
    };

    static async Task Main()
    {
        var playwright = await Playwright.CreateAsync();

        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--no-first-run",
                "--disable-extensions"
            }
        });

        var sitesJson = await File.ReadAllTextAsync("combined.json");
        var sites = JsonSerializer.Deserialize<List<string>>(sitesJson) ?? new List<string>();

        HashSet<string> processedSites = new();
        if (File.Exists("results.jsonl"))
        {
            foreach (var line in File.ReadAllLines("results.jsonl"))
            {
                try
                {
                    var siteInfo = JsonSerializer.Deserialize<SiteInfo>(line);
                    if (siteInfo != null && !string.IsNullOrEmpty(siteInfo.Url))
                        processedSites.Add(siteInfo.Url);
                }
                catch { }
            }
        }
        if (File.Exists("failed.jsonl"))
        {
            foreach (var line in File.ReadAllLines("failed.jsonl"))
            {
                try
                {
                    var url = JsonSerializer.Deserialize<string>(line);
                    if (!string.IsNullOrEmpty(url))
                        processedSites.Add(url);
                }
                catch { }
            }
        }

        sites = sites.Where(url => !processedSites.Contains(url)).ToList();

        using var stream = new FileStream("results.jsonl", FileMode.Append, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8
        };

        object writeLock = new object();
        int totalCount = sites.Count;
        int processedCount = 0;

        await Parallel.ForEachAsync(sites, parallelOptions, async (site, cancellationToken) =>
        {
            int maxRetries = 2;
            int attempt = 0;
            bool success = false;
            bool useProxy = false;

            while (attempt < maxRetries && !success && !cancellationToken.IsCancellationRequested)
            {
                attempt++;

                Proxy? playwrightProxy = null;
                if (useProxy)
                {
                    playwrightProxy = new Proxy { Server = vpsProxyServer };
                }

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                                "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 720, Height = 480 },
                    Locale = "ru-RU",
                    TimezoneId = "Europe/Moscow",
                    Proxy = playwrightProxy
                });

                try
                {
                    var page = await context.NewPageAsync();

                    await page.GotoAsync(site, new PageGotoOptions
                    {
                        Timeout = 30000,
                        WaitUntil = WaitUntilState.DOMContentLoaded
                    });

                    var metaDescription = await page.EvaluateAsync<string>(@"() => {
                        const el = document.querySelector('meta[name=""description""]');
                        return el ? el.getAttribute('content') : '';
                    }");

                    var (phones, emails, whatsapp, vk, instagram, telegram) = await FindContacts(page);

                    var title = await page.TitleAsync();

                    bool isErrorTitle = errorTitles.Any(err => title != null && title.Contains(err, StringComparison.OrdinalIgnoreCase));

                    bool isValidSite = !string.IsNullOrWhiteSpace(title) &&
                                       !isErrorTitle &&
                                       (!string.IsNullOrWhiteSpace(metaDescription) || phones.Count > 0 || emails.Count > 0);

                    if (!isValidSite)
                    {
                        throw new Exception($"Невалидный сайт или ошибка: {title}");
                    }
                    
                    var aboutUrl = await FindAboutPageAsync(page);
                    string aboutText = "";
                    if (!string.IsNullOrEmpty(aboutUrl))
                    {
                        var aboutPage = await context.NewPageAsync();
                        try
                        {
                            aboutText = await GetAboutAsync(aboutUrl, aboutPage);
                        }
                        finally
                        {
                            await aboutPage.CloseAsync();
                        }
                    }

                    var info = new SiteInfo
                    {
                        Url = site,
                        Title = title ?? "",
                        Desciption = metaDescription ?? "",
                        Tel = phones,
                        Email = emails,
                        Whatsapp = whatsapp,
                        Vk = vk,
                        Instagram = instagram,
                        Telegram = telegram,
                        About = aboutText
                    };

                    var jsonLine = JsonSerializer.Serialize(info, new JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = false
                    });

                    lock (writeLock)
                    {
                        writer.WriteLine(jsonLine);
                        writer.Flush();
                    }

                    int processed = Interlocked.Increment(ref processedCount);
                    Console.WriteLine($"Обработано: {processed} из {totalCount}, осталось: {totalCount - processed}");

                    await page.CloseAsync();
                    success = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{site}] Ошибка на попытке {attempt} {(useProxy ? "с прокси VPS" : "без прокси")}: {ex.Message}");

                    if (!useProxy)
                    {
                        useProxy = true;
                    }
                }
                finally
                {
                    await context.CloseAsync();
                }

                if (!success && attempt < maxRetries)
                {
                    await Task.Delay(3000, cancellationToken);
                }
            }

            if (!success)
            {
                Console.WriteLine($"[{site}] Не удалось обработать сайт после {maxRetries} попыток");

                string failedJson = JsonSerializer.Serialize(site);
                await File.AppendAllTextAsync("failed.jsonl", failedJson + Environment.NewLine);
            }
        });

        await browser.CloseAsync();
    }

    static public async Task<string?> FindAboutPageAsync(IPage page)
    {
        var anchors = await page.QuerySelectorAllAsync("a");
        foreach (var a in anchors)
        {
            var text = (await a.InnerTextAsync())?.Trim() ?? "";
            var href = (await a.GetAttributeAsync("href")) ?? "";

            if (aboutTexts.Any(about =>
                    text.Equals(about, StringComparison.OrdinalIgnoreCase) ||
                    text.IndexOf(about, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (string.IsNullOrWhiteSpace(href)
                    || href.StartsWith("javascript", StringComparison.OrdinalIgnoreCase)
                    || href.StartsWith("mailto", StringComparison.OrdinalIgnoreCase)
                    || href.StartsWith("tel", StringComparison.OrdinalIgnoreCase)
                    || href.StartsWith("#"))
                    continue;

                string url = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? href
                    : new Uri(new Uri(page.Url), href).ToString();
                return url;
            }
        }

        return null;
    }

    static public async Task<string> GetAboutAsync(string url, IPage page)
    {
        if (string.IsNullOrEmpty(url)
            || url.StartsWith("javascript", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("mailto", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("tel", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("#"))
        {
            return "";
        }

        try
        {
            await page.GotoAsync(url,
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });

            string elementsToRemove =
                "header, footer, nav, aside, .header, .footer, .sidebar, .navigation, .nav, .breadcrumbs, .search, .advertisement, .ads, [role='navigation'], [role='search']";

            await page.EvaluateAsync(@"(sel) => {
                    document.querySelectorAll(sel).forEach(e => e.remove());
                    document.querySelectorAll('input, button, select, textarea, script, style').forEach(e => e.remove());
                }", elementsToRemove);

            var text = await page.EvaluateAsync<string>(@"() => {
                    let content = '';
                    const selectors = ['main', 'section', 'article', 'div.about', 'div.company', 'body'];
                    for (let sel of selectors) {
                        const el = document.querySelector(sel);
                        if (el && el.innerText.trim().length > 100) {
                            content = el.innerText;
                            break;
                        }
                    }
                    return content.replace(/\n\s*\n/g, '\n').trim();
                }");

            if (!string.IsNullOrEmpty(text) && text.Length >= 100)
                return text;

            return "";
        }
        catch
        {
            return "";
        }
    }

    static public async Task<(List<string> phones, List<string> emails, List<string> whatsapp, List<string> vk, List<string> instagram, List<string> telegram)> FindContacts(IPage page)
    {
        var phones = new HashSet<string>();
        var emails = new HashSet<string>();
        var whatsapp = new HashSet<string>();
        var vk = new HashSet<string>();
        var instagram = new HashSet<string>();
        var telegram = new HashSet<string>();

        try
        {
            var phonesFromLinks = await page.EvaluateAsync<string[]>(@"
                () => Array.from(document.querySelectorAll('a[href^=""tel:""]')).map(a => a.getAttribute('href').replace(/^tel:/, '').trim())
            ");
            foreach (var p in phonesFromLinks)
                if (!string.IsNullOrWhiteSpace(p))
                    phones.Add(p);

            var emailsFromLinks = await page.EvaluateAsync<string[]>(@"
                () => Array.from(document.querySelectorAll('a[href^=""mailto:""]')).map(a => a.getAttribute('href').replace(/^mailto:/, '').trim())
            ");
            foreach (var e in emailsFromLinks)
                if (!string.IsNullOrWhiteSpace(e))
                    emails.Add(e);

            var telegramLinks = await page.EvaluateAsync<string[]>(@"
                () => Array.from(document.querySelectorAll('a[href*=""t.me""]')).map(a => a.getAttribute('href').trim())
            ");
            foreach (var t in telegramLinks)
                if (!string.IsNullOrWhiteSpace(t))
                    telegram.Add(t);

            var vkLinks = await page.EvaluateAsync<string[]>(@"
                () => Array.from(document.querySelectorAll('a[href*=""vk.com""]')).map(a => a.getAttribute('href').trim())
            ");
            foreach (var v in vkLinks)
                if (!string.IsNullOrWhiteSpace(v))
                    vk.Add(v);

            var waLinks = await page.EvaluateAsync<string[]>(@"
                () => Array.from(document.querySelectorAll('a[href*=""wa.me""], a[href*=""api.whatsapp.com""]')).map(a => a.getAttribute('href').trim())
            ");
            foreach (var w in waLinks)
                if (!string.IsNullOrWhiteSpace(w))
                    whatsapp.Add(w);

            var instaLinks = await page.EvaluateAsync<string[]>(@"
                () => Array.from(document.querySelectorAll('a[href*=""instagram.com""]')).map(a => a.getAttribute('href').trim())
            ");
            foreach (var i in instaLinks)
                if (!string.IsNullOrWhiteSpace(i))
                    instagram.Add(i);

            var pageContent = await page.EvaluateAsync<string>(@"() => document.body.innerText");
            if (!string.IsNullOrEmpty(pageContent))
            {
                var emailRegex = new System.Text.RegularExpressions.Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b");
                var matches = emailRegex.Matches(pageContent);
                foreach (System.Text.RegularExpressions.Match match in matches)
                    emails.Add(match.Value);
            }
        }
        catch
        {
        }

        return (phones.ToList(), emails.ToList(), whatsapp.ToList(), vk.ToList(), instagram.ToList(), telegram.ToList());
    }

    public class SiteInfo
    {
        public List<string> Tel { get; set; } = new();
        public List<string> Email { get; set; } = new();
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Desciption { get; set; } = "";
        public List<string> Vk { get; set; } = new();
        public List<string> Instagram { get; set; } = new();
        public List<string> Telegram { get; set; } = new();
        public List<string> Whatsapp { get; set; } = new();
        public string About { get; set; } = "";
    }
}