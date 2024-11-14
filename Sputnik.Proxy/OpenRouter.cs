using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;

namespace Sputnik.Proxy
{
    internal class OpenRouter
    {
        private static readonly HttpClient client = new HttpClient();

        public string ModelId { get; set; }

        public ResponseUsage LastUsage { get; private set; }

        public OpenRouter(string modelId)
        {
            ModelId = modelId;
        }

        /// <summary>
        /// Calculates price of last prompt based on the amount of used tokens and the current price.
        /// </summary>
        /// <returns>(Input price, Output price); Both can be summed together for the final price.</returns>
        public (decimal, decimal) CalculatePrice()
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

                JToken? model = json.SelectToken($"$.data[?(@.id == '{ModelId}')]");
                if (model == null)
                {
                    Console.WriteLine("Failed to fetch models.");
                    return result;
                }

                ResponsePricing pricing = JsonConvert.DeserializeObject<ResponsePricing>(model["pricing"]!.ToString())!;
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

        public async IAsyncEnumerable<string> Prompt(string prompt)
        {
            string systemPrompt = File.ReadAllText("Prompts\\Conversation.txt")
                .Replace(Environment.NewLine, " ")
                .Replace("\"", "\\\"");

            StringContent content = new StringContent(
                "{\"stream\": true, \"model\":\"" + ModelId + "\",\"messages\":[" +
                    "{\"role\":\"system\",\"content\":\"" + systemPrompt + "\"}," +
                    "{\"role\":\"user\",\"content\":\"" + prompt + "\"}]}",
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
                                LastUsage = JsonConvert.DeserializeObject<ResponseUsage>(chatResponse["usage"].ToString());
                            }

                            yield return (string)chatResponse["choices"][0]["delta"]["content"];
                        }
                    }
                }
            }
        }
    }
}
