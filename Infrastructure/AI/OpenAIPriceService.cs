using Core.Entities;
using Microsoft.Extensions.Http;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Infrastructure.AI;

public class OpenAIPriceService : IAIPriceService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public OpenAIPriceService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _http = httpClientFactory.CreateClient("OpenAI");
        _apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
    }

    public async Task<(decimal SuggestedPrice, string Reasoning)> GetPriceSuggestionAsync(
        Property property,
        string countryCode)
    {
        var prompt = BuildPrompt(property, countryCode);

        var request = new
        {
            model = "gpt-4o-mini",
            max_tokens = 500,
            messages = new[]
            {
                new { role = "system", content = GetSystemPrompt(countryCode) },
                new { role = "user",   content = prompt }
            }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Headers = { { "Authorization", $"Bearer {_apiKey}" } },
            Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
        };

        var response = await _http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(json);
        var content = result.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return ParseAIResponse(content, property.Price);
    }

    private static string GetSystemPrompt(string countryCode) => countryCode switch
    {
        "GB" => """
                You are a UK property valuation expert with deep knowledge of the London property market.
                Analyse the property details and suggest a fair market price in GBP.
                Consider: location/borough, property type, size, bedrooms, tenure (freehold/leasehold),
                EPC rating, tube zone, council tax band, and current London market conditions.
                Respond ONLY in this JSON format:
                {"suggestedPrice": 450000, "reasoning": "Brief explanation in 2-3 sentences"}
                """,
        "US" => """
                You are a US real estate valuation expert.
                Suggest a fair market price in USD based on the property details provided.
                Respond ONLY in JSON: {"suggestedPrice": 500000, "reasoning": "..."}
                """,
        "AE" => """
                You are a UAE property valuation expert familiar with Dubai and Abu Dhabi markets.
                Suggest a fair market price in AED based on the property details provided.
                Respond ONLY in JSON: {"suggestedPrice": 1500000, "reasoning": "..."}
                """,
        _ => "You are a property valuation expert. Suggest a fair price. Respond in JSON: {\"suggestedPrice\": 0, \"reasoning\": \"...\"}"
    };

    private static string BuildPrompt(Property property, string countryCode)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Please suggest a market price for this property:");
        sb.AppendLine($"- Type: {property.PropertyType}");
        sb.AppendLine($"- Listing: {property.ListingType}");
        sb.AppendLine($"- Location: {property.Region}, {property.City}, {property.PostalCode}");
        sb.AppendLine($"- Bedrooms: {property.Bedrooms ?? 0}");
        sb.AppendLine($"- Bathrooms: {property.Bathrooms ?? 0}");
        sb.AppendLine($"- Size: {property.SizeValue} {property.SizeUnit}");
        sb.AppendLine($"- Seller's asking price: {property.Price:N0}");

        if (!string.IsNullOrEmpty(property.CountryAttributes))
            sb.AppendLine($"- Additional details: {property.CountryAttributes}");

        return sb.ToString();
    }

    private static (decimal SuggestedPrice, string Reasoning) ParseAIResponse(string content, decimal fallbackPrice)
    {
        try
        {
            var json = JsonDocument.Parse(content);
            var price = json.RootElement.GetProperty("suggestedPrice").GetDecimal();
            var reason = json.RootElement.GetProperty("reasoning").GetString() ?? string.Empty;
            return (price, reason);
        }
        catch
        {
            // If parsing fails, return seller's price with a note
            return (fallbackPrice, "Price suggestion unavailable. Using seller's listed price as reference.");
        }
    }
}
