namespace Sputnik.Proxy.Models
{
    internal record GeneratedResponse
    {
        /// <summary>
        /// Prompt entered by the user.
        /// </summary>
        public required string UserPrompt { get; set; }

        /// <summary>
        /// Response may be null because it might not be generated yet.
        /// </summary>
        public string? Response { get; set; }
    }
}
