using Azure.AI.Translation.Text;
using Azure;
using HtmlAgilityPack;
using System.Xml.Linq;

/// <summary>
/// Provides functionality to translate entire .resx files using Azure Translator.
/// </summary>
public static class ResxTranslator
{
    /// <summary>
    /// Translates the values of a .resx file and writes them to the target file.
    /// </summary>
    public static async Task TranslateFileAsync(string subscriptionKey, string sourceFilePath, string targetFilePath, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(subscriptionKey) ||
            string.IsNullOrWhiteSpace(sourceFilePath) ||
            string.IsNullOrWhiteSpace(targetFilePath) ||
            string.IsNullOrWhiteSpace(targetLanguage))
        {
            Console.WriteLine("One or more required configuration values are missing.");
            return;
        }

        AzureKeyCredential credential = new(subscriptionKey);
        TextTranslationClient translationService;
        try
        {
            translationService = new TextTranslationClient(credential);
        }
        catch (Exception ex) when (ex is UriFormatException || ex is ArgumentException)
        {
            Console.WriteLine(ex is UriFormatException ? "The endpoint URL is not valid." : "The subscription key is not valid.");
            return;
        }

        if (!File.Exists(sourceFilePath))
        {
            Console.WriteLine($"Source file not found: {sourceFilePath}");
            return;
        }

        XDocument sourceDoc;
        try
        {
            sourceDoc = XDocument.Load(sourceFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load the source file as an XML document: {ex.Message}");
            return;
        }

        var dataElements = sourceDoc.Descendants("data").Where(x => x.Element("value") != null);
        if (!dataElements.Any())
        {
            Console.WriteLine("The source file does not contain any translatable elements.");
            return;
        }

        int total = dataElements.Count();
        int current = 0;

        foreach (var dataElement in dataElements)
        {
            current++;
            string sourceText = dataElement.Element("value")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(sourceText))
                continue;

            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(sourceText);

                foreach (var textNode in doc.DocumentNode.DescendantsAndSelf().Where(n => n.NodeType == HtmlNodeType.Text))
                {
                    string nodeText = textNode.InnerHtml;
                    var response = await translationService.TranslateAsync(targetLanguage, nodeText);
                    var result = response.Value.FirstOrDefault();
                    string translatedText = result?.Translations.FirstOrDefault()?.Text;
                    if (translatedText != null)
                    {
                        textNode.InnerHtml = translatedText;
                    }
                }

                string translatedHtml = doc.DocumentNode.OuterHtml;
                dataElement.Element("value")!.Value = translatedHtml;
                Program.ShowProgress(current, total);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"Translation failed. Error code: {ex.ErrorCode}, Message: {ex.Message}");
                return;
            }
        }

        sourceDoc.Save(targetFilePath);
        Console.WriteLine();
    }

    /// <summary>
    /// Translates only the missing values from the source .resx file into the target file.
    /// Existing entries in the target file are preserved.
    /// </summary>
    public static async Task TranslateMissingAsync(string subscriptionKey, string sourceFilePath, string targetFilePath, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(subscriptionKey) ||
            string.IsNullOrWhiteSpace(sourceFilePath) ||
            string.IsNullOrWhiteSpace(targetFilePath) ||
            string.IsNullOrWhiteSpace(targetLanguage))
        {
            Console.WriteLine("One or more required configuration values are missing.");
            return;
        }

        AzureKeyCredential credential = new(subscriptionKey);
        TextTranslationClient translationService;
        try
        {
            translationService = new TextTranslationClient(credential);
        }
        catch (Exception ex) when (ex is UriFormatException || ex is ArgumentException)
        {
            Console.WriteLine(ex is UriFormatException ? "The endpoint URL is not valid." : "The subscription key is not valid.");
            return;
        }

        if (!File.Exists(sourceFilePath))
        {
            Console.WriteLine($"Source file not found: {sourceFilePath}");
            return;
        }

        XDocument sourceDoc;
        try
        {
            sourceDoc = XDocument.Load(sourceFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load the source file as an XML document: {ex.Message}");
            return;
        }

        XDocument targetDoc = File.Exists(targetFilePath) ? XDocument.Load(targetFilePath) : new XDocument(new XElement("root"));

        var sourceData = sourceDoc.Descendants("data").Where(x => x.Element("value") != null);
        var targetLookup = targetDoc.Descendants("data").Where(x => x.Attribute("name") != null).ToDictionary(x => x.Attribute("name")!.Value);

        var missing = sourceData.Where(d =>
        {
            string key = d.Attribute("name")?.Value ?? string.Empty;
            return !targetLookup.ContainsKey(key);
        }).ToList();

        if (!missing.Any())
        {
            Console.WriteLine("No missing entries found.");
            return;
        }

        int total = missing.Count;
        int current = 0;

        foreach (var dataElement in missing)
        {
            current++;
            string sourceText = dataElement.Element("value")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(sourceText))
                continue;

            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(sourceText);

                foreach (var textNode in doc.DocumentNode.DescendantsAndSelf().Where(n => n.NodeType == HtmlNodeType.Text))
                {
                    string nodeText = textNode.InnerHtml;
                    var response = await translationService.TranslateAsync(targetLanguage, nodeText);
                    var result = response.Value.FirstOrDefault();
                    string translatedText = result?.Translations.FirstOrDefault()?.Text;
                    if (translatedText != null)
                    {
                        textNode.InnerHtml = translatedText;
                    }
                }

                string translatedHtml = doc.DocumentNode.OuterHtml;
                var newElement = new XElement("data",
                    new XAttribute("name", dataElement.Attribute("name")?.Value ?? string.Empty),
                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement("value", translatedHtml));
                targetDoc.Root!.Add(newElement);
                Program.ShowProgress(current, total);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"Translation failed. Error code: {ex.ErrorCode}, Message: {ex.Message}");
                return;
            }
        }

        targetDoc.Save(targetFilePath);
        Console.WriteLine();
    }
}
