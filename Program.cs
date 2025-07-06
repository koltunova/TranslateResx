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

        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var cmdArgs = args.Skip(1).ToArray();

        switch (command)
        {
            case "translate":
                await RunTranslate(cmdArgs, configuration);
                break;
            case "cleanup":
                RunCleanup(cmdArgs);
                break;
            default:
                PrintHelp();
                break;
        }

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("TranslateResx CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  translate   Translate resource files");
        Console.WriteLine("  cleanup     Remove entries not present in example file");
        Console.WriteLine();
        Console.WriteLine("translate options:");
        Console.WriteLine("  --all             translate all items");
        Console.WriteLine("  --missing         translate only missing items");
        Console.WriteLine("  --source <file>   path to source Strings.resx");
        Console.WriteLine("  --target <file>   path to target resx file");
        Console.WriteLine("  --language <lang> target language");
        Console.WriteLine();
        Console.WriteLine("cleanup options:");
        Console.WriteLine("  --example <file>  example resx file");
        Console.WriteLine("  --files <files>   resx files to clean separated by space");
    }

    private static async Task RunTranslate(string[] args, IConfiguration configuration)
    {
        bool all = args.Contains("--all");
        bool missing = args.Contains("--missing");
        string? sourceFilePath = GetOption(args, "--source") ?? configuration["Files:Source"];
        string? targetFilePath = GetOption(args, "--target") ?? configuration["Files:Target"];
        string? targetLanguage = GetOption(args, "--language") ?? configuration["Translation:TargetLanguage"];
        string? subscriptionKey = configuration["AzureTranslation:SubscriptionKey"];

        Console.WriteLine($"Source file: {sourceFilePath}");
        Console.WriteLine($"Target file: {targetFilePath}");
        Console.WriteLine($"Language: {targetLanguage}");
        Console.WriteLine(all ? "Translate all items" : "Translate missing items");

        // TODO: implement support for the command options
        await TranslateResxFile(subscriptionKey, sourceFilePath, targetFilePath, targetLanguage);
    }

    private static void RunCleanup(string[] args)
    {
        string? exampleFile = GetOption(args, "--example");
        var files = GetOptions(args, "--files");

        Console.WriteLine($"Example file: {exampleFile}");
        Console.WriteLine("Files to clean:");
        foreach (var f in files)
        {
            Console.WriteLine($" - {f}");
        }

        Console.WriteLine("Cleanup functionality is not implemented yet.");
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        if (index >= 0 && index < args.Length - 1)
        {
            return args[index + 1];
        }
        return null;
    }

    private static IEnumerable<string> GetOptions(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        if (index >= 0)
        {
            return args.Skip(index + 1).TakeWhile(a => !a.StartsWith("--"));
        }
        return Array.Empty<string>();
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
