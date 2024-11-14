# Sputnik proxy
Venera can't directly run LLM inference and trying to port such tools to our platform is a impossible task. However,
CosmosOS directly supports networking which we can use for remote inference. This is what this sub-project is about: proxying.

CosmosOS only supports raw TCP and UDP connections and no HTTP(S). Therefore, this proxy spawns a TCP server that the
[Sputnik client]() within our OS can connect to.

## Cloud inference
This proxy connects to remote AI infrastructure though [OpenRouter](https://openrouter.ai) ([Privacy Policy](https://openrouter.ai/privacy)).
It gives us access to a whole range of models, including, but not limited to, ChatGPT, Llama and NVIDIA's Nemotron
for cheap money. The Sputnik proxy holds the actual API key and not the client. The provided API key will be limited
in terms of billing. OpenRouter will forward our request to various different cloud providers anonymously while
keeping no logs themselves (hopefully 🙏).