using Newtonsoft.Json;

namespace Sputnik.Proxy.Models
{
    internal class VeneraUserInfo
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Username})";
        }
    }
}
