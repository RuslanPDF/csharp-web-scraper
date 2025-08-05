using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CombinedProcessingApp
{
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
        static readonly string[] DomainDisabledPhrases = new[]
        {
            "домен выставлен на продажу",
            "домен заблокирован",
            "домен заблокирован на время",
            "домен заблокирован по решению регистратора",
            "домен заблокирован регистратором",
            "домен временно заблокирован",
            "домен временно приостановлен",
            "домен не активен",
            "домен не найден",
            "домен не зарегистрирован",
            "домен отключен",
            "домен отключён",
            "домен припаркован",
            "домен приостановлен",
            "домен находится в статусе clienthold",
            "домен находится в статусе redemptionperiod",
            "домен находится в статусе serverhold",
            "домен в статусе clienthold",
            "домен в статусе pendingdelete",
            "домен в статусе redemptionperiod",
            "домен в статусе serverhold",
            "истек срок делегирования домена",
            "истек срок регистрации домена",
            "истекла регистрация домена",
            "приостановлен доступ к сайту",
            "приостановлена регистрация домена",
            "регистрация домена приостановлена",
            "сайт временно недоступен",
            "сайт временно отключён",
            "сайт заблокирован",
            "сайт недоступен",
            "сайт не работает",
            "сайт отключен",
            "сайт отключён",
            "сайт приостановлен",
            "сервис временно недоступен",
            "сервис недоступен",
            "страница блокировки домена",
            "страница не найдена",
            "страница парковки домена",
            "страница временной приостановки домена",
            "этот домен не обслуживается",
            "error 404",
            "ошибка 404",
            "веб-сайт не найден",
            "сайт снят с публикации",
            "доступ к домену временно ограничен",
            "временная блокировка домена",
            "доменное имя временно заблокировано",
            "роскомнадзор",
            "заблокирован",
        };

        static readonly string[] AboutDisabledPhrases = new[]
        {
            "429 too many requests",
            "too many requests",
            "error 429",

            "ваш ip адрес",
            "ip address",
            "ip заблокирован",
            "ip заблокирован на",
            "ip заблокирован на 300 сек",
            "ip заблокирован на 300сек",
            "ip заблокирован на 5 минут",
            "ip временно заблокирован",
            "ip временно заблокирован на",
            "ip заблокирован временно",
            "ip address blocked",
            "ip blocked",
            "ip temporarily blocked",

            "ispmanager",
            "ispsystem",
            "isp system",

            "не будет заблокирован, пока вы авторизованы",
            "will not be blocked as long as you are logged in",

            "заблокирован",
            "blocked",
            "блокировка",
            "блокирует",
            "блокирует на",
            "ошибка",
            "error",
        };

        static void Main()
        {
            try
            {
                string name = "service and trade";
                string inputJsonPath = $"combine_results/valid_description_about.json";
                string outputFilteredPath = "filtered_output.json";
                string outputCleanSitesPath = "clean_sites.json";
                string outputValidDescPath = $"{name}/valid_description_about.json";
                string outputInvalidDescPath = $"{name}/invalid_description_about.json";

                Console.WriteLine("Читаем весь JSON файл...");

                var jsonContent = File.ReadAllText(inputJsonPath);
                var jsonArray = JArray.Parse(jsonContent);

                var nonEmptyEmailItems = new JArray();
                var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                Console.WriteLine("Фильтруем записи с непустыми уникальными Email...");

                foreach (JObject item in jsonArray)
                {
                    var emailArray = item["Email"] as JArray;
                    if (emailArray == null || emailArray.Count == 0) continue;

                    var cleanedEmails = emailArray
                        .Select(emailToken =>
                        {
                            var email = emailToken.ToString();
                            int idx = email.IndexOf('?');
                            return idx >= 0 ? email.Substring(0, idx) : email;
                        })
                        .Where(email => !string.IsNullOrWhiteSpace(email))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var uniqueEmails = cleanedEmails.Where(email => !seenEmails.Contains(email)).ToList();

                    if (uniqueEmails.Count == 0) continue;

                    foreach (var email in uniqueEmails)
                        seenEmails.Add(email);

                    item["Email"] = new JArray(uniqueEmails);
                    nonEmptyEmailItems.Add(item);
                }

                File.WriteAllText(outputFilteredPath, nonEmptyEmailItems.ToString(Formatting.Indented));
                Console.WriteLine($"Сохранено {nonEmptyEmailItems.Count} объектов с уникальными непустыми Email.");

                var sites = JsonConvert.DeserializeObject<List<SiteInfo>>(nonEmptyEmailItems.ToString());

                if (sites == null || sites.Count == 0)
                {
                    Console.WriteLine("Нет доступных для обработки сайтов.");
                    return;
                }

                Console.WriteLine("Фильтруем сайты по признакам недоступности...");

                var cleanSites = sites.Where(site =>
                    (string.IsNullOrWhiteSpace(site.Title) ||
                     !DomainDisabledPhrases.Any(phrase =>
                         site.Title.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
                    &&
                    (string.IsNullOrWhiteSpace(site.About) ||
                     !AboutDisabledPhrases.Any(phrase =>
                         site.About.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
                ).ToList();

                File.WriteAllText(outputCleanSitesPath, JsonConvert.SerializeObject(cleanSites, Formatting.Indented));
                Console.WriteLine($"Оставлено {cleanSites.Count} сайтов без признаков блокировки или ошибки.");

                Console.WriteLine("Разделяем сайты на те, у которых заполнено Description или About, и нет...");

                var validItems = cleanSites.Where(site =>
                    !string.IsNullOrWhiteSpace(site.Desciption) || !string.IsNullOrWhiteSpace(site.About)).ToList();

                var invalidItems = cleanSites.Except(validItems).ToList();

                File.WriteAllText(outputValidDescPath, JsonConvert.SerializeObject(validItems, Formatting.Indented));
                File.WriteAllText(outputInvalidDescPath,
                    JsonConvert.SerializeObject(invalidItems, Formatting.Indented));

                Console.WriteLine($"Сохранено {validItems.Count} объектов с описанием.");
                Console.WriteLine($"Сохранено {invalidItems.Count} объектов без описания.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Произошла ошибка при обработке:");
                Console.WriteLine(ex);
            }
        }
    }
}