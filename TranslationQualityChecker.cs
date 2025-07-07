using Azure.AI.Translation.Text;
using Azure;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Provides functionality to evaluate translation quality using a round-trip translation.
/// </summary>
public static class TranslationQualityChecker
{
    /// <summary>
    /// Performs a back-translation check for each entry and returns a list of confidence scores.
    /// </summary>
    public static async Task<List<(string Key, int Rating)>> CheckQualityAsync(string subscriptionKey, string sourceFilePath, string targetFilePath, string sourceLanguage, string targetLanguage)
    {
        var results = new List<(string Key, int Rating)>();

        if (string.IsNullOrWhiteSpace(subscriptionKey) ||
            string.IsNullOrWhiteSpace(sourceFilePath) ||
            string.IsNullOrWhiteSpace(targetFilePath))
        {
            Console.WriteLine("Missing configuration values for quality check.");
            return results;
        }

        if (!File.Exists(sourceFilePath) || !File.Exists(targetFilePath))
        {
            Console.WriteLine("Source or target file not found for quality check.");
            return results;
        }

        XDocument sourceDoc = XDocument.Load(sourceFilePath);
        XDocument targetDoc = XDocument.Load(targetFilePath);
        var sourceData = sourceDoc.Descendants("data").Where(x => x.Element("value") != null);
        var targetLookup = targetDoc.Descendants("data").Where(x => x.Element("value") != null).ToDictionary(e => e.Attribute("name")?.Value ?? string.Empty);

        AzureKeyCredential credential = new(subscriptionKey);
        TextTranslationClient client = new TextTranslationClient(credential);

        foreach (var data in sourceData)
        {
            string key = data.Attribute("name")?.Value ?? string.Empty;
            if (!targetLookup.TryGetValue(key, out var targetElement))
                continue;

            string originalText = data.Element("value")?.Value ?? string.Empty;
            string translatedText = targetElement.Element("value")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(translatedText))
                continue;

            var response = await client.TranslateAsync(sourceLanguage, translatedText, targetLanguage);
            var result = response.Value.FirstOrDefault();
            string backTranslated = result?.Translations.FirstOrDefault()?.Text ?? string.Empty;

            double score = Similarity(originalText, backTranslated);
            int rating = ScoreToRating(score);
            results.Add((key, rating));
        }

        return results;
    }

    private static double Similarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
            return 1.0;
        int distance = LevenshteinDistance(s1, s2);
        int maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 1.0;
        return 1.0 - ((double)distance / maxLen);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static int ScoreToRating(double score)
    {
        if (score >= 0.90) return 5;
        if (score >= 0.75) return 4;
        if (score >= 0.50) return 3;
        if (score >= 0.25) return 2;
        return 1;
    }
}
