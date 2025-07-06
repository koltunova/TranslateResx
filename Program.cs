using Azure.AI.Translation.Text;
using Azure;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Xml.Linq;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLocalization();
        services.AddSingleton(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            // Load "Strings" resources using the L namespace
            return factory.Create("Strings", typeof(L.Strings).Assembly.FullName);
        });

        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<IStringLocalizer>();

        var supportedCultureCodes = configuration.GetSection("Localization:SupportedCultures").Get<string[]>() ?? Array.Empty<string>();
        var exampleLanguages = configuration.GetSection("Localization:ExampleLanguages").Get<string[]>() ?? Array.Empty<string>();

        CultureInfo[] supportedCultures = supportedCultureCodes.Select(code => new CultureInfo(code)).ToArray();

        Console.WriteLine("Supported cultures:");
        foreach (var culture in supportedCultures)
        {
            if (LanguageData.Names.TryGetValue(culture.Name, out var display))
            {
                Console.WriteLine($" - {culture.Name} ({display})");
            }
            else
            {
                Console.WriteLine($" - {culture.Name}");
            }
        }
        Console.WriteLine("Example languages:");
        foreach (var lang in exampleLanguages)
        {
            if (LanguageData.Names.TryGetValue(lang, out var name))
            {
                Console.WriteLine($" - {lang} ({name})");
            }
            else
            {
                Console.WriteLine($" - {lang}");
            }
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Select an option:");
            Console.WriteLine("1. Translate resource file");
            Console.WriteLine("2. Cleanup resource files");
            Console.WriteLine("3. Quit");
            Console.Write(" > ");

            var choice = Console.ReadLine()?.Trim().ToLowerInvariant();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                case "translate":
                    await RunTranslateInteractive(configuration);
                    break;
                case "2":
                case "cleanup":
                    RunCleanupInteractive();
                    break;
                case "3":
                case "q":
                case "quit":
                    return 0;
                default:
                    Console.WriteLine("Unknown option. Please try again.");
                    break;
            }
        }
    }

    private static async Task RunTranslateInteractive(IConfiguration configuration)
    {
        string defaultSource = configuration["Files:Source"] ?? string.Empty;
        string defaultTarget = configuration["Files:Target"] ?? string.Empty;
        string defaultLanguage = configuration["Translation:TargetLanguage"] ?? string.Empty;
        string? subscriptionKey = configuration["AzureTranslation:SubscriptionKey"];

        Console.Write($"Source file [{defaultSource}]: ");
        string? sourceFilePath = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            sourceFilePath = defaultSource;

        Console.Write($"Target file [{defaultTarget}]: ");
        string? targetFilePath = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(targetFilePath))
            targetFilePath = defaultTarget;

        Console.Write($"Language [{defaultLanguage}]: ");
        string? targetLanguage = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(targetLanguage))
            targetLanguage = defaultLanguage;

        Console.Write("Translate all items? (y/N): ");
        var allInput = Console.ReadLine();
        bool all = allInput?.Trim().ToLowerInvariant().StartsWith("y") == true;

        Console.WriteLine();
        Console.WriteLine($"Source file: {sourceFilePath}");
        Console.WriteLine($"Target file: {targetFilePath}");
        Console.WriteLine($"Language: {targetLanguage}");
        Console.WriteLine(all ? "Translate all items" : "Translate missing items");

        await TranslateResxFile(subscriptionKey, sourceFilePath, targetFilePath, targetLanguage);
    }

    private static void RunCleanupInteractive()
    {
        Console.Write("Source file: ");
        string? sourceFile = Console.ReadLine();

        Console.Write("Target files (space separated): ");
        string? targets = Console.ReadLine();
        var targetFiles = (targets ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        RunCleanup(sourceFile, targetFiles);
    }

    private static void RunCleanup(string? sourceFile, IEnumerable<string> targetFiles)
    {
        Console.WriteLine($"Source file: {sourceFile}");
        Console.WriteLine("Target files:");
        foreach (var f in targetFiles)
        {
            Console.WriteLine($" - {f}");
        }

        Console.WriteLine("Cleanup functionality is not implemented yet.");
    }


    private static async Task TranslateResxFile(string? subscriptionKey, string? sourceFilePath, string? targetFilePath, string? targetLanguage)
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
    }
}
