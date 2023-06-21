using Azure.AI.Translation.Text;
using System.Xml.Linq;
using HtmlAgilityPack;
using Azure;

// Define your Azure Translation service credentials and file paths
string subscriptionKey = "your_subscription_key";
string subscriptionKey = "<your_subscription_key_here>";
string sourceFilePath = "<path_to_your_source_file_here>";
string targetFilePath = "<path_to_your_target_file_here>";
string targetLanguage = "<target_language_here>";

// Create an AzureKeyCredential object using your subscription key
AzureKeyCredential credential = new(subscriptionKey);

// Initialize the TextTranslationClient with your credentials
TextTranslationClient translationService;

try
{
    translationService = new TextTranslationClient(credential);
}
catch (UriFormatException)
{
    Console.WriteLine("The endpoint URL is not valid.");
    return;
}
catch (ArgumentException)
{
    Console.WriteLine("The subscription key is not valid.");
    return;
}

// Check if the source file exists
if (!File.Exists(sourceFilePath))
{
    Console.WriteLine($"Source file not found: {sourceFilePath}");
    return;
}

// Load the source .resx file as an XDocument
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

// Select all "data" elements that have a "value" child element
var dataElements = sourceDoc.Descendants("data")
                            .Where(x => x.Element("value") != null);

if (!dataElements.Any())
{
    Console.WriteLine("The source file does not contain any translatable elements.");
    return;
}

// Iterate over each "data" element
foreach (var dataElement in dataElements)
{
    // Get the source text from the "value" element
    string sourceText = dataElement.Element("value")?.Value;

    if (sourceText == null)
    {
        continue;
    }

    // Translate the source text
    try
    {
        // Create a new HtmlDocument and load the source text into it
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(sourceText);

        // Iterate over each text node in the HtmlDocument
        foreach (var textNode in doc.DocumentNode.DescendantsAndSelf().Where(n => n.NodeType == HtmlNodeType.Text))
        {
            string nodeText = textNode.InnerHtml;
            var response = await translationService.TranslateAsync(targetLanguage, nodeText);
            var result = response.Value.FirstOrDefault();
            string translatedText = result?.Translations.FirstOrDefault()?.Text;

            // Replace the text in the text node with the translated text
            if (translatedText != null)
            {
                textNode.InnerHtml = translatedText;
            }
        }

        // Get the translated HTML
        string translatedHtml = doc.DocumentNode.OuterHtml;

        // Replace the text in the "value" element with the translated text
        dataElement.Element("value").Value = translatedHtml;

        // Output the source text and the translated text
        Console.WriteLine($"Source Text: {sourceText}");
        Console.WriteLine($"Translated Text: {translatedHtml}");
    }
    catch (RequestFailedException ex)
    {
        Console.WriteLine($"Translation failed. Error code: {ex.ErrorCode}, Message: {ex.Message}");
        return;
    }
}

// Save the modified XDocument to the target file
sourceDoc.Save(targetFilePath);
