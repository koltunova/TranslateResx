using Azure.AI.Translation.Text;
using Azure;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Xml.Linq;

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

        // Display the configured languages so the user knows what is
        // available. LanguageData.Names maps the language code to a friendly
        // display name.
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
        // The example languages list is shorter and intended to show
        // beginners which languages they might want to try translating into.
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

        // Start a simple menu loop so the user can choose what to do next.
        // This loop continues until the user selects the Quit option.
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
        string defaultResourcesPath = configuration["Files:ResourcesPath"] ?? string.Empty;
        string? subscriptionKey = configuration["AzureTranslation:SubscriptionKey"];

        // Ask the user for the folder that contains the Strings.<culture>.resx files.
        Console.Write($"Resources directory [{defaultResourcesPath}]: ");
        string? resourcesDir = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(resourcesDir))
            resourcesDir = defaultResourcesPath;

        // The UI now builds file names based on the languages the user enters.
        Console.Write("Source language code (e.g. en-US): ");
        string? sourceLanguage = Console.ReadLine();

        Console.Write("Target language codes (space separated): ");
        string? targetLanguagesInput = Console.ReadLine();
        var targetLanguages = (targetLanguagesInput ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (string.IsNullOrWhiteSpace(resourcesDir) ||
            string.IsNullOrWhiteSpace(sourceLanguage) ||
            targetLanguages.Length == 0)
        {
            Console.WriteLine("Resource directory and languages are required.");
            return;
        }

        string sourceFilePath = Path.Combine(resourcesDir, $"Strings.{sourceLanguage}.resx");

        foreach (var lang in targetLanguages)
        {
            string targetFilePath = Path.Combine(resourcesDir, $"Strings.{lang}.resx");
            Console.WriteLine();
            Console.WriteLine($"Source file: {sourceFilePath}");
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
