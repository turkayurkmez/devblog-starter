# word-counter-mcp

FastMCP ile yazilmis, verilen bir metindeki toplam kelime sayisini donduren tek araclik (tool) bir MCP server.

## Kurulum

```bash
cd mcp-servers/word-counter-mcp
python -m venv .venv
.venv\Scripts\activate      # Windows
pip install -r requirements.txt
```

## Claude Code'a Ekleme

```bash
claude mcp add word-counter -- python "mcp-servers/word-counter-mcp/server.py"
```

Alternatif olarak FastMCP'nin kendi kurulum komutuyla (bagimliliklari da otomatik yonetir):

```bash
fastmcp install claude-code server.py --with fastmcp
```

Ekli sunuculari listelemek / kaldirmak icin:

```bash
claude mcp list
claude mcp remove word-counter
```

## Kullanim

Claude Code icinde `count_words` aracini bir metinle cagirmaniz yeterli, orn: "Bu metindeki kelime sayisini say: ...".

Server'i tek basina calistirip test etmek icin:

```bash
python server.py
```
