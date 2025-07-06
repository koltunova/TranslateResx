using Azure.AI.Translation.Text;
using Azure;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Xml.Linq;

using System.IO;
/// <summary>
/// Entry point and main workflow for the TranslateResx console application.
/// The program allows you to translate .resx resource files using the
/// Azure Translator service. An interactive console menu guides the user
/// through translating files or cleaning up obsolete resources. The code is
/// heavily commented to help you understand how each step works.
/// </summary>
class Program
{
    /// <summary>
    /// Application entry point. Configures dependency injection, reads
    /// application settings and then starts a simple console menu loop.
    /// The method returns a task because translation operations are
    /// asynchronous.
    /// </summary>
    static async Task<int> Main(string[] args)
    {
        // Determine which configuration file to use based on the
        // DOTNET_ENVIRONMENT environment variable. When nothing is
        // specified we fall back to "Production" settings.
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        // Build the configuration object that will read settings from
        // appsettings.json and an optional environment specific file
        // like appsettings.Development.json.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Set up dependency injection. Here we only register the
        // localization services which provide access to .resx files.
        var services = new ServiceCollection();
        services.AddLogging(); // <-- Add this line to register logging
        services.AddLocalization();
        services.AddSingleton(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            // Load "Strings" resources using the L namespace
            return factory.Create("Strings", typeof(L.Strings).Assembly.FullName);
        });

        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<IStringLocalizer>();

        // Read the list of languages that our application supports and a
        // shorter list of example languages that will be displayed to the
        // user. These values come from appsettings.json.
        var supportedCultureCodes = configuration.GetSection("Localization:SupportedCultures").Get<string[]>() ?? Array.Empty<string>();
        var exampleLanguages = configuration.GetSection("Localization:ExampleLanguages").Get<string[]>() ?? Array.Empty<string>();

        // Convert the string codes like "en-US" into CultureInfo objects so
        // that .NET can handle formatting and resource lookups correctly.
        CultureInfo[] supportedCultures = supportedCultureCodes.Select(code => new CultureInfo(code)).ToArray();

        // Save the resources path so it can be reused when the menu is shown.
        string resourcesPath = configuration["Files:ResourcesPath"] ?? string.Empty;

        // Start a simple menu loop so the user can choose what to do next.
        // This loop continues until the user selects the Quit option.
        while (true)
        {
            Console.WriteLine();
            // Display the configured languages table every time the menu appears
            // so the user can easily see available resources.
            PrintLanguagesTable(supportedCultures, resourcesPath);

            // The example languages list is shorter and intended to show beginners
            // which languages they might want to try translating into.
            Console.WriteLine();
            Console.WriteLine("Example languages: " + string.Join(", ", exampleLanguages
                .Select(l => LanguageData.Names.TryGetValue(l, out var n) ? $"{l} ({n})" : l)));

            Console.WriteLine();
            Console.WriteLine("Select an option:");
            Console.WriteLine("1. Translate resource file");
            Console.WriteLine("2. Cleanup resource files");
            Console.WriteLine("3. Quit");
            Console.Write(" > ");

            var choice = Console.ReadLine()?.Trim().ToLowerInvariant();
            Console.WriteLine();

            // React to the user's choice. Each menu item invokes a helper
            // method to keep Main clean and easy to read.
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

    /// <summary>
    /// Asks the user for the details required to translate a resource file
    /// and then calls <see cref="TranslateResxFile"/> with those values.
    /// </summary>
    private static async Task RunTranslateInteractive(IConfiguration configuration)
    {
        string resourcesDir = configuration["Files:ResourcesPath"] ?? string.Empty;
        string defaultSourceLanguage = "en";
        string? subscriptionKey = configuration["AzureTranslation:SubscriptionKey"];

        // Show the user which resources directory will be used.
        Console.WriteLine($"Resources directory: {resourcesDir}");

        // The UI now builds file names based on the languages the user enters.
        Console.Write($"Source language code [{defaultSourceLanguage}]: ");
        string? sourceLanguage = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(sourceLanguage))
            sourceLanguage = defaultSourceLanguage;

        Console.Write("Target language codes (space separated): ");
        string? targetLanguagesInput = Console.ReadLine();
        var targetLanguages = (targetLanguagesInput ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (string.IsNullOrWhiteSpace(resourcesDir) ||
            targetLanguages.Length == 0)
        {
            Console.WriteLine("Resource directory and languages are required.");
            return;
        }

        string sourceFilePath = GetExistingResourceFilePath(resourcesDir, sourceLanguage, true);
        Console.WriteLine($"Source file: {sourceFilePath}");

        foreach (var lang in targetLanguages)
        {
            string targetFilePath = GetExistingResourceFilePath(resourcesDir, lang, true);
            Console.WriteLine();
            Console.WriteLine($"Target file: {targetFilePath}");
            Console.WriteLine($"Language: {lang}");

            await TranslateResxFile(subscriptionKey, sourceFilePath, targetFilePath, lang);
        }
    }

    /// <summary>
    /// Prompts the user for a source .resx file and a list of target files
    /// that should be compared against it. This method simply gathers input
    /// and passes it to <see cref="RunCleanup"/>.
    /// </summary>
    private static void RunCleanupInteractive()
    {
        Console.Write("Source file: ");
        string? sourceFile = Console.ReadLine();

        Console.Write("Target files (space separated): ");
        string? targets = Console.ReadLine();
        var targetFiles = (targets ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        RunCleanup(sourceFile, targetFiles);
    }

    /// <summary>
    /// Placeholder for cleanup functionality. In a real application this
    /// would remove resources from the target files that do not exist in the
    /// source file.
    /// </summary>
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

    private static void PrintLanguagesTable(IEnumerable<CultureInfo> cultures, string resourcesPath)
    {
        Console.WriteLine("Supported cultures:");
        Console.WriteLine($"{"Code",-10} {"Name",-30} {"File",-20} {"Size",10} {"Updated",-20}");
        Console.WriteLine(new string('-', 92));
        foreach (var culture in cultures)
        {
            string name = LanguageData.Names.TryGetValue(culture.Name, out var display) ? display : culture.DisplayName;
            string fileName = string.Empty;
            string size = string.Empty;
            string updated = string.Empty;
            if (!string.IsNullOrWhiteSpace(resourcesPath))
            {
                string path = GetExistingResourceFilePath(resourcesPath, culture.Name, true);
                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    fileName = info.Name;
                    size = info.Length.ToString();
                    updated = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                }
            }
            Console.WriteLine($"{culture.Name,-10} {name,-30} {fileName,-20} {size,10} {updated,-20}");
        }
    }

    private static string GetExistingResourceFilePath(string baseDir, string languageCode, bool fallBackToDefault = false)
    {
        string fullPath = Path.Combine(baseDir, $"Strings.{languageCode}.resx");
        if (File.Exists(fullPath))
            return fullPath;

        try
        {
            var culture = new CultureInfo(languageCode);
            if (!string.Equals(culture.Name, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            {
                string shortPath = Path.Combine(baseDir, $"Strings.{culture.TwoLetterISOLanguageName}.resx");
                if (File.Exists(shortPath))
                    return shortPath;
            }
        }
        catch (CultureNotFoundException)
        {
            // Ignore invalid culture codes and just return the original path
        }

        if (fallBackToDefault)
        {
            try
            {
                var culture = new CultureInfo(languageCode);
                if (culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase))
                {
                    string defaultPath = Path.Combine(baseDir, "Strings.resx");
                    if (File.Exists(defaultPath))
                        return defaultPath;
                }
            }
            catch (CultureNotFoundException)
            {
                // Ignore invalid culture codes and proceed
            }
        }

        return fullPath;
    }


    /// <summary>
    /// Reads a source .resx file, sends each string to the Azure Translator
    /// service and saves the results into a new .resx file.
    /// </summary>
    private static async Task TranslateResxFile(string? subscriptionKey, string? sourceFilePath, string? targetFilePath, string? targetLanguage)
    {
        // Validate the critical parameters before proceeding. Missing values
        // would cause the translation service to fail or the files to be
        // inaccessible.
        if (string.IsNullOrWhiteSpace(subscriptionKey) ||
            string.IsNullOrWhiteSpace(sourceFilePath) ||
            string.IsNullOrWhiteSpace(targetFilePath) ||
            string.IsNullOrWhiteSpace(targetLanguage))
        {
            Console.WriteLine("One or more required configuration values are missing.");
            return;
        }

        // The Azure SDK uses AzureKeyCredential to pass the subscription key.
        AzureKeyCredential credential = new(subscriptionKey);

        // Create the client that communicates with Azure. Any issues with
        // the endpoint or credentials will throw an exception that we catch
        // to show a friendly error message.
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

        // Make sure the source file exists before trying to load it.
        if (!File.Exists(sourceFilePath))
        {
            Console.WriteLine($"Source file not found: {sourceFilePath}");
            return;
        }

        // Load the XML file that contains our strings. If the file is not a
        // valid XML document we stop and let the user know.
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

        // Each translatable string is stored in a <data> element with a
        // <value> child. The LINQ query filters out any entries without text.
        var dataElements = sourceDoc.Descendants("data")
                                    .Where(x => x.Element("value") != null);

        if (!dataElements.Any())
        {
            Console.WriteLine("The source file does not contain any translatable elements.");
            return;
        }

        // Process each string one by one.
        foreach (var dataElement in dataElements)
        {
            string sourceText = dataElement.Element("value")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(sourceText))
                continue;

            try
            {
                // Some strings may contain HTML markup. HtmlAgilityPack helps
                // us parse the markup so that we only translate the text
                // inside the tags.
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

                // Replace the original text with the translated version.
                string translatedHtml = doc.DocumentNode.OuterHtml;
                dataElement.Element("value")!.Value = translatedHtml;

                // Show the user each translation so they can follow progress.
                Console.WriteLine($"Source Text: {sourceText}");
                Console.WriteLine($"Translated Text: {translatedHtml}");
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"Translation failed. Error code: {ex.ErrorCode}, Message: {ex.Message}");
                return;
            }
        }

        // Finally save all translations to the target .resx file.
        sourceDoc.Save(targetFilePath);
    }
}
