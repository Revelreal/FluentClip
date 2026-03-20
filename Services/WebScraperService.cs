using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace FluentClip.Services;

public class WebScraperService
{
    private readonly HttpClient _httpClient;
    private readonly string _logFilePath;

    private static readonly List<HotSite> HotSites = new()
    {
        new HotSite { Name = "知乎", Domain = "zhihu.com", SearchUrl = "https://www.zhihu.com/search?q=", IsForum = true },
        new HotSite { Name = "微博", Domain = "weibo.com", SearchUrl = "https://s.weibo.com/weibo?q=", IsForum = true },
        new HotSite { Name = "百度", Domain = "baidu.com", SearchUrl = "https://www.baidu.com/s?wd=", IsForum = false },
        new HotSite { Name = "bilibili", Domain = "bilibili.com", SearchUrl = "https://search.bilibili.com/all?keyword=", IsForum = true },
        new HotSite { Name = "CSDN", Domain = "csdn.net", SearchUrl = "https://so.csdn.net/so/search?q=", IsForum = true },
        new HotSite { Name = "掘金", Domain = "juejin.cn", SearchUrl = "https://juejin.cn/search?query=", IsForum = true },
        new HotSite { Name = "简书", Domain = "jianshu.com", SearchUrl = "https://www.jianshu.com/search?q=", IsForum = true },
        new HotSite { Name = "博客园", Domain = "cnblogs.com", SearchUrl = "https://zzk.cnblogs.com/s?q=", IsForum = true },
        new HotSite { Name = "今日头条", Domain = "toutiao.com", SearchUrl = "https://www.toutiao.com/search/?keyword=", IsForum = false },
        new HotSite { Name = "腾讯新闻", Domain = "qq.com", SearchUrl = "https://search.qq.com/keyword?searchword=", IsForum = false },
    };

    public WebScraperService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, $"webscraper_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    private void Log(string message)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            Console.WriteLine(logEntry);
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
        }
        catch { }
    }

    public static List<HotSite> GetHotSites() => HotSites;

    public async Task<SearchResult> SearchAndScrapeAsync(string query, int maxResults = 3, CancellationToken cancellationToken = default)
    {
        var result = new SearchResult { Query = query };

        try
        {
            Log($"[DEBUG] 开始搜索: {query}");

            var searchUrls = await SearchOnSitesAsync(query, cancellationToken);
            result.SearchUrls = searchUrls;

            if (searchUrls.Count == 0)
            {
                result.ErrorMessage = "未找到相关结果";
                return result;
            }

            var scrapeTasks = searchUrls.Take(maxResults).Select(url => ScrapeUrlAsync(url, cancellationToken));
            var scrapedContents = await Task.WhenAll(scrapeTasks);

            result.ScrapedPages = scrapedContents.Where(p => p != null).ToList()!;

            Log($"[DEBUG] 搜索完成，找到 {searchUrls.Count} 个URL，抓取了 {result.ScrapedPages.Count} 个页面");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 搜索抓取失败: {ex.Message}");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<List<string>> SearchOnSitesAsync(string query, CancellationToken cancellationToken)
    {
        var urls = new List<string>();

        var siteTasks = HotSites.Select(async site =>
        {
            try
            {
                var searchUrl = site.SearchUrl + Uri.EscapeDataString(query);
                var isValid = await CheckSiteAccessibleAsync(searchUrl, cancellationToken);
                return (site, searchUrl, isValid);
            }
            catch
            {
                return (site, site.SearchUrl, false);
            }
        });

        var results = await Task.WhenAll(siteTasks);

        foreach (var (site, searchUrl, isValid) in results)
        {
            if (isValid)
            {
                urls.Add(searchUrl);
                Log($"[DEBUG] {site.Name} 可访问: {searchUrl}");
            }
        }

        Log($"[DEBUG] 共 {urls.Count} 个网站可访问");
        return urls;
    }

    private async Task<bool> CheckSiteAccessibleAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ScrapedPage?> ScrapeUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            Log($"[DEBUG] 开始抓取: {url}");

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = ExtractTitle(doc);
            var content = ExtractMainContent(doc, url);
            var summary = GenerateSummary(content);

            Log($"[DEBUG] 抓取完成: {title}, 内容长度: {content.Length}");

            return new ScrapedPage
            {
                Url = url,
                Title = title,
                Content = content,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 抓取失败 {url}: {ex.Message}");
            return null;
        }
    }

    private string ExtractTitle(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode != null)
        {
            return CleanText(titleNode.InnerText);
        }

        var h1Node = doc.DocumentNode.SelectSingleNode("//h1");
        if (h1Node != null)
        {
            return CleanText(h1Node.InnerText);
        }

        var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
        if (ogTitle != null && ogTitle.Attributes["content"] != null)
        {
            return CleanText(ogTitle.Attributes["content"].Value);
        }

        return "未知标题";
    }

    private string ExtractMainContent(HtmlDocument doc, string url)
    {
        var sb = new StringBuilder();

        RemoveUnwantedNodes(doc);

        string? siteName = null;
        foreach (var site in HotSites)
        {
            if (url.Contains(site.Domain))
            {
                siteName = site.Name;
                break;
            }
        }

        var contentSelectors = GetContentSelectors(siteName);

        foreach (var selector in contentSelectors)
        {
            var nodes = doc.DocumentNode.SelectNodes(selector);
            if (nodes != null && nodes.Count > 0)
            {
                foreach (var node in nodes)
                {
                    var text = ExtractTextFromNode(node);
                    if (text.Length > 50)
                    {
                        sb.AppendLine(text);
                    }
                }
                break;
            }
        }

        if (sb.Length == 0)
        {
            var body = doc.DocumentNode.SelectSingleNode("//body");
            if (body != null)
            {
                sb.Append(ExtractTextFromNode(body));
            }
        }

        return sb.ToString().Trim();
    }

    private List<string> GetContentSelectors(string? siteName)
    {
        return siteName switch
        {
            "知乎" => new List<string>
            {
                "//div[@class='List-item']",
                "//div[@class='ContentItem']",
                "//div[@class='RichText']",
                "//div[@class='feed-content']",
                "//div[@class='post-content']"
            },
            "微博" => new List<string>
            {
                "//div[@class='content']",
                "//div[@class='WB_text']",
                "//div[@class='feed_detail']",
                "//div[@class='detail']"
            },
            "百度" => new List<string>
            {
                "//div[@class='result']",
                "//div[@class='result-op']",
                "//div[@class='c-abstract']",
                "//div[@class='content-right']"
            },
            "bilibili" => new List<string>
            {
                "//div[@class='video-card']",
                "//div[@class='report']",
                "//div[@class='content']",
                "//div[@class='desc']"
            },
            "CSDN" => new List<string>
            {
                "//div[@class='article_content']",
                "//div[@class='blog-content']",
                "//div[@class='post-content']",
                "//pre[@class='pre']"
            },
            "掘金" => new List<string>
            {
                "//div[@class='search-content']",
                "//div[@class='article-item']",
                "//div[@class='content']"
            },
            "简书" => new List<string>
            {
                "//div[@class='note-list']",
                "//div[@class='article']",
                "//div[@class='content']"
            },
            _ => new List<string>
            {
                "//article",
                "//main",
                "//div[@class='content']",
                "//div[@class='main-content']",
                "//div[@class='post-content']",
                "//div[@class='article-content']",
                "//div[@class='entry-content']",
                "//div[contains(@class, 'content')]"
            }
        };
    }

    private void RemoveUnwantedNodes(HtmlDocument doc)
    {
        var unwantedSelectors = new[]
        {
            "//script",
            "//style",
            "//nav",
            "//header",
            "//footer",
            "//aside",
            "//iframe",
            "//noscript",
            "//form",
            "//button",
            "//input",
            "//select",
            "//textarea",
            "//div[contains(@class, 'nav')]",
            "//div[contains(@class, 'menu')]",
            "//div[contains(@class, 'sidebar')]",
            "//div[contains(@class, 'footer')]",
            "//div[contains(@class, 'header')]",
            "//div[contains(@class, 'advertisement')]",
            "//div[contains(@class, 'ad-')]",
            "//div[contains(@class, 'social')]",
            "//div[contains(@class, 'share')]",
            "//div[contains(@class, 'comment')]",
            "//div[contains(@class, 'related')]",
            "//div[contains(@class, 'popup')]",
            "//div[contains(@class, 'modal')]"
        };

        foreach (var selector in unwantedSelectors)
        {
            var nodes = doc.DocumentNode.SelectNodes(selector);
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    node.Remove();
                }
            }
        }
    }

    private string ExtractTextFromNode(HtmlNode node)
    {
        var sb = new StringBuilder();

        foreach (var child in node.DescendantsAndSelf())
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                var text = child.InnerText.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(text);
                    sb.Append(" ");
                }
            }
            else if (child.Name == "p" || child.Name == "h1" || child.Name == "h2" ||
                     child.Name == "h3" || child.Name == "h4" || child.Name == "h5" ||
                     child.Name == "h6" || child.Name == "li" || child.Name == "br")
            {
                sb.AppendLine();
            }
        }

        var result = CleanText(sb.ToString());
        result = Regex.Replace(result, @"\s+", " ");
        result = Regex.Replace(result, @"\n\s*\n", "\n\n");

        return result.Trim();
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[\r\n]+", "\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n\s+", "\n");

        return text.Trim();
    }

    private string GenerateSummary(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        var sentences = Regex.Split(content, @"[。.!?！？\n]+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(5)
            .ToList();

        return string.Join("。", sentences) + "。";
    }

    public string FormatForAI(SearchResult result)
    {
        if (result.ScrapedPages.Count == 0)
        {
            return $"未找到关于「{result.Query}」的相关信息喵~";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🔍 搜索「{result.Query}」相关网页内容：");
        sb.AppendLine();

        foreach (var page in result.ScrapedPages)
        {
            sb.AppendLine($"📄 **{page.Title}**");
            sb.AppendLine($"🔗 {page.Url}");
            sb.AppendLine();
            sb.AppendLine("【内容摘要】");
            sb.AppendLine(page.Summary);
            sb.AppendLine();
            sb.AppendLine("【主要内容】");
            var truncatedContent = page.Content.Length > 2000
                ? page.Content.Substring(0, 2000) + "..."
                : page.Content;
            sb.AppendLine(truncatedContent);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public class SearchResult
{
    public string Query { get; set; } = "";
    public List<string> SearchUrls { get; set; } = new();
    public List<ScrapedPage> ScrapedPages { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class ScrapedPage
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class HotSite
{
    public string Name { get; set; } = "";
    public string Domain { get; set; } = "";
    public string SearchUrl { get; set; } = "";
    public bool IsForum { get; set; }
}
