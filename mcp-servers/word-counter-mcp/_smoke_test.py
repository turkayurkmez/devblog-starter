import asyncio

from fastmcp import Client

client = Client("server.py")


async def main() -> None:
    async with client:
        result = await client.call_tool("count_words", {"text": "Selam Dunya"})
        print(result)


asyncio.run(main())
