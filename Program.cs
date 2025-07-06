using Azure.AI.Translation.Text;
using Azure;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

string? subscriptionKey = configuration["AzureTranslation:SubscriptionKey"];
string? sourceFilePath = configuration["Files:Source"];
string? targetFilePath = configuration["Files:Target"];
string? targetLanguage = configuration["Translation:TargetLanguage"];

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
    Console.WriteLine(ex is UriFormatException
        ? "The endpoint URL is not valid."
        : "The subscription key is not valid.");
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

var dataElements = sourceDoc.Descendants("data")
                            .Where(x => x.Element("value") != null);

if (!dataElements.Any())
{
    Console.WriteLine("The source file does not contain any translatable elements.");
    return;
}

foreach (var dataElement in dataElements)
{
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

        Console.WriteLine($"Source Text: {sourceText}");
        Console.WriteLine($"Translated Text: {translatedHtml}");
    }
    catch (RequestFailedException ex)
    {
        Console.WriteLine($"Translation failed. Error code: {ex.ErrorCode}, Message: {ex.Message}");
        return;
    }
}

sourceDoc.Save(targetFilePath);
