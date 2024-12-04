# Sputnik proxy
This project is directly related to [Venera](https://github.com/ebsksjk/Venera), a operating system developed with the
[Cosmos](https://www.gocosmos.org/) framework for a project at the Duale Hochschule Gera-Eisenach.

Venera can't directly run LLM inference and trying to port such tools to our platform is an impossible task. However,
CosmosOS directly supports networking which we can use for remote inference. This is what this sub-project is about: proxying.

CosmosOS only supports raw TCP and UDP connections and no HTTP(S). Therefore, this proxy spawns a TCP server that the
[Sputnik client](https://github.com/ebsksjk/Venera/blob/master/Shell/Programs/Sputnik.cs) within our OS can connect to.

## Cloud inference
This proxy connects to remote AI infrastructure though [OpenRouter](https://openrouter.ai) ([Privacy Policy](https://openrouter.ai/privacy)).
It gives us access to a whole range of models, including, but not limited to, OpenAI, Llama and Claude 3.5
for cheap money. The Sputnik proxy holds the actual API key and not the client. OpenRouter will forward our request
to various different cloud providers anonymously while
keeping no logs themselves (hopefully 🙏).

## Installation
This project is not intented to be hosted by anyone and is only used during the small presentation period of Venera.
If you still want to host it, here is how I do it:
- Clone this repository.
- Open the project solution in Visual Studio 2022.
- Publish as self-contained project and choose `linux-x64` as target.
- Zip the contents of the export & upload to your server.
- Unzip into a folder called `server/` and build a Docker image using this `Dockerfile` in the parent folder:
```dockerfile
FROM debian:bookworm
RUN apt update -y && apt install libicu-dev -y

COPY server/. /opt/

WORKDIR /opt
ENTRYPOINT ["/opt/Sputnik.Proxy"]
```
- Put into the same folder (where your `Dockerfile` is), this `docker-compose.yml`:
```yml
services:
  sputnik:
    image: sputnik
    build: .
    restart: always
    volumes:
      - /etc/ssl/certs:/etc/ssl/certs:ro
    environment:
      - OPENROUTER_API_KEY=sk-or-v1-abc123
      - PROXY_PSK=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
      - DEBUG=false
      - TZ=Europe/Berlin
    ports:
      - 9999:9999
```
- This should be it. Have fun.