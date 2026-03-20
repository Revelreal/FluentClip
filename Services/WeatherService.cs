using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace FluentClip.Services;

public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _logFilePath;

    private static readonly string[] WeatherSites = new[]
    {
        "https://www.weather.com.cn/weather/{cityCode}.shtml",
        "https://www.baidu.com/s?wd={city}天气"
    };

    public WeatherService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, $"weather_{DateTime.Now:yyyyMMdd_HHmmss}.log");
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

    public async Task<string> GetWeatherAsync(string city)
    {
        try
        {
            Log($"[DEBUG] 查询天气: {city}");

            var result = await QueryFromWeatherCom(city);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }

            result = await QueryFromBaidu(city);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }

            return $"抱歉，无法获取 {city} 的天气信息喵~ 可能是因为网络问题或者该城市不存在 nya~";
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 查询天气失败: {ex.Message}");
            return $"查询天气失败惹喵~ 错误信息: {ex.Message}";
        }
    }

    private async Task<string> QueryFromWeatherCom(string city)
    {
        try
        {
            var cityCode = GetCityCode(city);
            if (string.IsNullOrEmpty(cityCode))
            {
                return "";
            }

            var url = $"https://www.weather.com.cn/weather/{cityCode}.shtml";
            Log($"[DEBUG] 请求天气网: {url}");

            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var today = doc.DocumentNode.SelectSingleNode("//div[@class='today']");
            if (today == null)
            {
                today = doc.DocumentNode.SelectSingleNode("//div[@class='weather']");
            }

            if (today == null)
            {
                var weatherInfo = ExtractWeatherInfoFromText(html);
                if (!string.IsNullOrEmpty(weatherInfo))
                {
                    return FormatWeatherResult(city, weatherInfo);
                }
                return "";
            }

            var temp = today.SelectSingleNode(".//span[@class='tem']")?.InnerText ??
                       today.SelectSingleNode(".//p[@class='tem']")?.InnerText ?? "";
            var weather = today.SelectSingleNode(".//p[@class='wea']")?.InnerText ?? "";
            var wind = today.SelectSingleNode(".//p[@class='win']")?.InnerText ?? "";
            var tips = today.SelectSingleNode(".//p[@class='tips']")?.InnerText ?? "";

            temp = CleanText(temp);
            weather = CleanText(weather);
            wind = CleanText(wind);
            tips = CleanText(tips);

            Log($"[DEBUG] 天气信息: {temp}, {weather}, {wind}");

            var result = $"🌤️ **{city}今日天气**\n\n";
            result += $"🌡️ 温度: {temp}\n";
            result += $"☁️ 天气: {weather}\n";
            result += $"💨 风力: {wind}\n";
            if (!string.IsNullOrEmpty(tips))
            {
                result += $"💡 提示: {tips}\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            Log($"[ERROR] Weather.com 查询失败: {ex.Message}");
            return "";
        }
    }

    private async Task<string> QueryFromBaidu(string city)
    {
        try
        {
            var url = $"https://www.baidu.com/s?wd={Uri.EscapeDataString(city + "天气")}";
            Log($"[DEBUG] 请求百度: {url}");

            var html = await _httpClient.GetStringAsync(url);

            var weatherInfo = ExtractWeatherInfoFromText(html);
            if (!string.IsNullOrEmpty(weatherInfo))
            {
                return FormatWeatherResult(city, weatherInfo);
            }

            var resultModule = html.IndexOf("天气");
            if (resultModule > 0)
            {
                var start = Math.Max(0, resultModule - 500);
                var end = Math.Min(html.Length, resultModule + 500);
                var section = html.Substring(start, end - start);

                var tempMatch = Regex.Match(section, @"(\d+)°");
                var weatherMatch = Regex.Match(section, @"(晴|多云|阴|雨|雪|雾|霾|风|雷)");

                if (tempMatch.Success || weatherMatch.Success)
                {
                    var result = $"🔍 **{city}天气查询结果**\n\n";
                    if (tempMatch.Success)
                    {
                        result += $"🌡️ 温度: {tempMatch.Value}\n";
                    }
                    if (weatherMatch.Success)
                    {
                        result += $"☁️ 天气: {weatherMatch.Value}\n";
                    }
                    result += "\n💡 以上信息来自百度搜索结果喵~ 🐾";
                    return result;
                }
            }

            return "";
        }
        catch (Exception ex)
        {
            Log($"[ERROR] 百度查询失败: {ex.Message}");
            return "";
        }
    }

    private string ExtractWeatherInfoFromText(string html)
    {
        var patterns = new[]
        {
            @"(\d+)℃",
            @"(-?\d+)度",
            @"(晴|多云|阴|小雨|中雨|大雨|暴雨|雷阵雨|小雪|中雪|大雪|暴雪|雾|霾|沙尘暴)",
            @"(东风|南风|西风|北风|东南风|东北风|西南风|西北风)(\d+)级",
            @"(紫外线|空气|湿度|气压|能见度)[^<\n]*"
        };

        var results = new System.Collections.Generic.List<string>();

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(html, pattern);
            foreach (Match match in matches)
            {
                if (!string.IsNullOrEmpty(match.Value) && match.Value.Length > 1)
                {
                    results.Add(match.Value);
                }
            }
        }

        return string.Join("\n", results.Take(10));
    }

    private string FormatWeatherResult(string city, string info)
    {
        if (string.IsNullOrEmpty(info))
        {
            return $"抱歉，无法获取 {city} 的详细天气信息喵~";
        }

        var result = $"🔍 **{city}天气**\n\n";
        result += info.Replace("\n", "\n") + "\n\n";
        result += "💡 以上信息来自网页搜索喵~ 🐾";

        return result;
    }

    private string GetCityCode(string city)
    {
        var cityCodes = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "北京", "101010100" },
            { "上海", "101020100" },
            { "广州", "101280101" },
            { "深圳", "101280601" },
            { "杭州", "101210101" },
            { "南京", "101190101" },
            { "武汉", "101200101" },
            { "成都", "101270101" },
            { "重庆", "101040100" },
            { "西安", "101110101" },
            { "天津", "101030100" },
            { "苏州", "101190401" },
            { "郑州", "101180101" },
            { "长沙", "101250101" },
            { "沈阳", "101070101" },
            { "青岛", "101120201" },
            { "大连", "101070201" },
            { "厦门", "101230201" },
            { "宁波", "101210401" },
            { "昆明", "101290101" },
            { "哈尔滨", "101050101" },
            { "长春", "101060101" },
            { "福州", "101230101" },
            { "南昌", "101240101" },
            { "贵阳", "101260101" },
            { "太原", "101100101" },
            { "石家庄", "101090101" },
            { "济南", "101120101" },
            { "兰州", "101160101" },
            { "呼和浩特", "101080101" },
            { "乌鲁木齐", "101130101" },
            { "拉萨", "101140101" },
            { "银川", "101170101" },
            { "西宁", "101150101" },
            { "海口", "101310101" },
            { "三亚", "101310201" },
            { "东莞", "101281601" },
            { "佛山", "101280800" },
            { "无锡", "101190201" },
            { "常州", "101191101" },
            { "徐州", "101190801" },
            { "温州", "101210501" },
            { "嘉兴", "101210501" },
            { "绍兴", "101210501" },
            { "金华", "101210901" },
            { "台州", "101211001" },
            { "湖州", "101210201" },
            { "扬州", "101190601" },
            { "南通", "101190701" },
            { "连云港", "101191001" },
            { "淮安", "101190901" },
            { "盐城", "101191201" },
            { "镇江", "101190301" },
            { "泰州", "101191101" },
            { "宿迁", "101191301" }
        };

        foreach (var kvp in cityCodes)
        {
            if (city.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return "";
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }
}
