using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sputnik.Proxy.Models;
using System.Globalization;
using System.Text;

namespace Sputnik.Proxy;

public enum TalkingStyle
{
    Kind = 1,
    Mixed = 2,
    Rude = 3,
    Raw = 4,
    Calc = 5
}

internal class OpenRouter
{
    private static readonly HttpClient client = new HttpClient();

    public ResponseUsage LastUsage { get; private set; }

    public OpenRouter()
    {
    }

    /// <summary>
    /// Calculates price of last prompt based on the amount of used tokens and the current price.
    /// </summary>
    /// <returns>(Input price, Output price); Both can be summed together for the final price.</returns>
    public (decimal, decimal) CalculatePrice(string model)
    {
        (decimal, decimal) result = (0, 0);

        try
        {
            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://openrouter.ai/api/v1/models"),
                Headers =
            {
                { "Accept", "application/json" }
            }
            };

            HttpResponseMessage response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            string rawJson = response.Content.ReadAsStringAsync().Result;
            JObject json = JObject.Parse(rawJson);

            JToken? fetchedModel = json.SelectToken($"$.data[?(@.id == '{model}')]");
            if (fetchedModel == null)
            {
                Console.WriteLine("Failed to fetch models.");
                return result;
            }

            ResponsePricing pricing = JsonConvert.DeserializeObject<ResponsePricing>(fetchedModel["pricing"]!.ToString())!;
            decimal promptCost = decimal.Parse(pricing.prompt, CultureInfo.InvariantCulture);
            decimal generationCost = decimal.Parse(pricing.completion, CultureInfo.InvariantCulture);

            //Console.WriteLine($"{promptCost} | {generationCost} ; Used: {LastUsage.PromptTokens} | {LastUsage.CompletionTokens}");
            decimal finalGenerationCost = LastUsage.CompletionTokens * generationCost;
            decimal finalPromptCost = LastUsage.PromptTokens * promptCost;

            result = (finalPromptCost, finalGenerationCost);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error occured on price calculation: {e.ToString()}");
        }

        return result;
    }

    public string GetModelByStyle(TalkingStyle style)
    {
        return style switch
        {
            TalkingStyle.Kind => "mistralai/mistral-large-2411",
            TalkingStyle.Mixed => "mistralai/mistral-nemo",
            TalkingStyle.Rude => "mistralai/mistral-nemo",
            TalkingStyle.Calc => "google/gemini-pro-1.5",
            TalkingStyle.Raw => "anthropic/claude-3.5-haiku",
            _ => "mistralai/mistral-nemo"
        };
    }

    public async IAsyncEnumerable<string> Prompt(TalkingStyle style, string prompt, List<GeneratedResponse>? prevContext, VeneraUserInfo userInfo)
    {
        string systemPrompt = File.ReadAllText($"Prompts{Path.DirectorySeparatorChar}SystemPrompt.txt")
            .Replace(Environment.NewLine, " ")
            .Replace("\"", "\\\"");

        switch (style)
        {
            case TalkingStyle.Kind:
                string kindStyle = File.ReadAllText($"Prompts{Path.DirectorySeparatorChar}Kind.txt")
                    .Replace(Environment.NewLine, " ")
                    .Replace("\"", "\\\"");

                systemPrompt = systemPrompt.Replace("%style%", kindStyle);
                break;
            case TalkingStyle.Mixed:
                string mixedStyle = File.ReadAllText($"Prompts{Path.DirectorySeparatorChar}Mixed.txt")
                    .Replace(Environment.NewLine, " ")
                    .Replace("\"", "\\\"");
                systemPrompt = systemPrompt.Replace("%style%", mixedStyle);
                break;
            case TalkingStyle.Rude:
                string rudeStyle = File.ReadAllText($"Prompts{Path.DirectorySeparatorChar}Rude.txt")
                    .Replace(Environment.NewLine, " ")
                    .Replace("\"", "\\\"");
                systemPrompt = systemPrompt.Replace("%style%", rudeStyle);
                break;
            case TalkingStyle.Raw:
                systemPrompt = "";
                break;
            case TalkingStyle.Calc:
                string calcStyle = File.ReadAllText($"Prompts{Path.DirectorySeparatorChar}Calculator.txt")
                    .Replace(Environment.NewLine, " ")
                    .Replace("\"", "\\\"");
                systemPrompt = calcStyle;
                break;
        }

        // Add user info to prompt
        systemPrompt = systemPrompt.Replace("%name%", userInfo.Name).Replace(
            "%username%", userInfo.Username);

        string jsonContext = string.Empty;

        /*
         * Build conversation dialog.
         * Not required for raw as it meant for smart command and is a "one-shot" thing.
         * Not required for calc as it's not a conversation and each prompt is self contained.
         */
        if ((style != TalkingStyle.Raw && style != TalkingStyle.Calc) && prevContext != null)
        {
            for (int i = 0; i < prevContext.Count; i++)
            {
                jsonContext += GenerateChat(prevContext[i]) + ",";
            }
        }

        // Pick the correct LLM model for each style.
        string model = GetModelByStyle(style);


        string jsonContent = "{\"stream\": true, \"max_tokens\": 512, \"model\":\"" + model + "\",\"messages\":[" +
                "{\"role\":\"system\",\"content\":" + JsonConvert.ToString(systemPrompt) + "}," +
                jsonContext +
                "{\"role\":\"user\",\"content\":" + JsonConvert.ToString(prompt) + "}]}";

        //Logging.LogDebug($"Final request (style: {style}): {jsonContent}");
        //Logging.LogDebug($"Forward: \"{JsonConvert.DeserializeObject(jsonContent).ToString()}\"");

        StringContent content = new StringContent(
            jsonContent,
            Encoding.UTF8,
            "application/json"
        );

        HttpRequestMessage request;

        try
        {
            request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://openrouter.ai/api/v1/chat/completions"),
                Headers =
            {
                { "Authorization", $"Bearer {Program.OR_API_KEY}" },
                { "Accept", "text/event-stream" }
            },
                Content = content
            };
        }
        catch (Exception)
        {
            yield break;
        }

        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        // Read the response stream continuously for SSE
        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var reader = new StreamReader(stream))
        {
            bool firstToken = true;
            while (!reader.EndOfStream)
            {
                string line = await reader.ReadLineAsync();

                // Process the line as an SSE event if it's not empty
                if (!string.IsNullOrWhiteSpace(line))
                {
                    if (line.StartsWith("data:"))
                    {
                        // Strip the "data:" prefix
                        string json = line.Substring(5).Trim();

                        JObject chatResponse;
                        try
                        {
                            // Deserialize the JSON data to ChatResponse object
                            chatResponse = JObject.Parse(json);
                        }
                        catch (JsonReaderException)
                        {
                            yield break;
                        }

                        if (chatResponse["usage"] != null)
                        {
                            LastUsage = JsonConvert.DeserializeObject<ResponseUsage>(chatResponse["usage"]!.ToString())!;
                        }

                        string token = (string)chatResponse?["choices"]?[0]?["delta"]?["content"];

                        if (token == null)
                        {
                            yield return string.Empty;
                        }

                        // Some LLMs put spaces in front of their responses (looking at you Mistral Nemo). So we trim
                        // the first returned token at the beginning.
                        if (firstToken)
                        {
                            token = token.TrimStart();
                            firstToken = false;
                        }

                        yield return token;
                    }
                }
            }
        }
    }

    private string GenerateChat(GeneratedResponse context)
    {
        string result = "{\"role\":\"user\",\"content\":" + JsonConvert.ToString(context.UserPrompt) + "}";

        if (context.Response != null)
        {
            result += ", {\"role\":\"assistant\",\"content\":" + JsonConvert.ToString(context.Response) + "}";
        }

        return result;
    }
}
