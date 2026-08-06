"""
docs/ altindaki makaleleri chunk'lar, Voyage AI ile embedding'lerini alir ve
backend'in DocChunks tablosuna (devblog.db) yazar.

Chunking stratejisi langchain_chunking_demo.py ile aynidir: once
MarkdownHeaderTextSplitter ile baslik sinirlarindan bolunur, sonra
RecursiveCharacterTextSplitter ile paragraf/cumle/kelime hiyerarsisiyle
kucultulur.

Kullanim:
    VOYAGE_API_KEY=... py embed_and_store.py
"""

import json
import os
import re
import sqlite3
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import requests
from langchain_text_splitters import (
    MarkdownHeaderTextSplitter,
    RecursiveCharacterTextSplitter,
)

DOCS_DIR = Path(__file__).parent.parent
DB_PATH = Path(__file__).parent.parent.parent / "backend" / "src" / "DevBlog.Api" / "devblog.db"
VOYAGE_MODEL = "voyage-3.5"
VOYAGE_URL = "https://api.voyageai.com/v1/embeddings"
CHUNK_SIZE = 1000
CHUNK_OVERLAP = 150
# Odeme yontemi eklenmemis Voyage hesaplari 3 RPM / 10K TPM ile sinirlidir;
# kucuk batch + istekler arasi bekleme + 429'da backoff bu limitlere gore ayarlandi.
EMBED_BATCH_SIZE = 15
BATCH_DELAY_SECONDS = 20
MAX_RETRIES = 5


def find_source_files() -> list[Path]:
    pattern = re.compile(r"^\d{2}-.*\.md$")
    files = [p for p in DOCS_DIR.iterdir() if p.is_file() and pattern.match(p.name)]
    return sorted(files)


def chunk_file(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8")

    header_splitter = MarkdownHeaderTextSplitter(
        headers_to_split_on=[("#", "h1"), ("##", "h2")],
        strip_headers=False,
    )
    header_sections = header_splitter.split_text(text)

    recursive_splitter = RecursiveCharacterTextSplitter(
        chunk_size=CHUNK_SIZE,
        chunk_overlap=CHUNK_OVERLAP,
        separators=["\n\n", "\n", ". ", " ", ""],
    )
    final_chunks = recursive_splitter.split_documents(header_sections)

    return [c.page_content.strip() for c in final_chunks if c.page_content.strip()]


def embed_batch(texts: list[str], api_key: str) -> list[list[float]]:
    for attempt in range(1, MAX_RETRIES + 1):
        response = requests.post(
            VOYAGE_URL,
            headers={"Authorization": f"Bearer {api_key}"},
            json={"input": texts, "model": VOYAGE_MODEL, "input_type": "document"},
            timeout=60,
        )
        if response.status_code == 429 and attempt < MAX_RETRIES:
            wait_seconds = int(response.headers.get("Retry-After", 30))
            print(f"429 alindi, {wait_seconds}s beklenip yeniden denenecek (deneme {attempt}/{MAX_RETRIES}).")
            time.sleep(wait_seconds)
            continue
        response.raise_for_status()
        data = response.json()["data"]
        data.sort(key=lambda item: item["index"])
        return [item["embedding"] for item in data]
    raise RuntimeError("Embedding istegi tekrar denemelere ragmen basarisiz oldu.")


def main() -> None:
    api_key = os.environ.get("VOYAGE_API_KEY")
    if not api_key:
        sys.exit("VOYAGE_API_KEY ortam degiskeni tanimli degil.")

    files = find_source_files()
    if not files:
        sys.exit(f"{DOCS_DIR} altinda 'NN-*.md' desenine uyan dosya bulunamadi.")

    rows: list[tuple[str, int, str]] = []
    for path in files:
        chunks = chunk_file(path)
        source_file = f"docs/{path.name}"
        for index, content in enumerate(chunks):
            rows.append((source_file, index, content))

    print(f"Kaynak dosya sayisi: {len(files)}")
    print(f"Toplam chunk sayisi: {len(rows)}")

    all_embeddings: list[list[float]] = []
    texts = [content for _, _, content in rows]
    for start in range(0, len(texts), EMBED_BATCH_SIZE):
        if start > 0:
            time.sleep(BATCH_DELAY_SECONDS)
        batch = texts[start : start + EMBED_BATCH_SIZE]
        embeddings = embed_batch(batch, api_key)
        all_embeddings.extend(embeddings)
        print(f"Embed edildi: {start + len(batch)}/{len(texts)}")

    created_at = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S.%f")

    con = sqlite3.connect(DB_PATH)
    try:
        con.executemany(
            """
            INSERT INTO DocChunks (SourceFile, ChunkIndex, Content, VectorJson, CreatedAt)
            VALUES (?, ?, ?, ?, ?)
            """,
            [
                (source_file, index, content, json.dumps(embedding), created_at)
                for (source_file, index, content), embedding in zip(rows, all_embeddings)
            ],
        )
        con.commit()
    finally:
        con.close()

    print(f"\n{len(rows)} chunk, DocChunks tablosuna yazildi ({DB_PATH}).")


if __name__ == "__main__":
    main()
