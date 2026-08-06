"""
Basit chunking demosu.

RAG'in ilk adimi olan chunking'i gosterir: bir markdown dokumanini
sabit boyutlu, kelime sinirlarini bozmayan ve ardisik parcalar arasinda
ortusme (overlap) birakan kucuk parcalara boler.

Kullanim:
    py chunking_demo.py
    py chunking_demo.py --file ../10-rag-mimarisi.md --size 400 --overlap 80
"""

import argparse
from dataclasses import dataclass
from pathlib import Path


@dataclass
class Chunk:
    index: int
    start: int
    end: int
    text: str


def chunk_text(text: str, size: int = 400, overlap: int = 80) -> list[Chunk]:
    """Metni `size` karakterlik parcalara boler.

    Parca siniri bir kelimenin ortasina denk gelirse, siniri en yakin
    bosluga geri cekerek kelimeyi bolmemeye calisir. Ardisik parcalar
    arasinda `overlap` karakter kadar ortusme birakilir, boylece sinir
    noktasindaki baglam her iki parcada da bulunur.
    """
    if overlap >= size:
        raise ValueError("overlap, size'dan kucuk olmali")

    chunks: list[Chunk] = []
    start = 0
    text_length = len(text)
    index = 0

    while start < text_length:
        end = min(start + size, text_length)

        if end < text_length:
            boundary = text.rfind(" ", start, end)
            if boundary > start:
                end = boundary

        chunk_content = text[start:end].strip()
        if chunk_content:
            chunks.append(Chunk(index=index, start=start, end=end, text=chunk_content))
            index += 1

        if end >= text_length:
            break

        start = end - overlap

    return chunks


def main() -> None:
    parser = argparse.ArgumentParser(description="Basit sabit-boyutlu chunking demosu")
    parser.add_argument(
        "--file",
        default=str(Path(__file__).parent.parent / "10-rag-mimarisi.md"),
        help="Chunk'lanacak dosya (varsayilan: docs/10-rag-mimarisi.md)",
    )
    parser.add_argument("--size", type=int, default=400, help="Chunk boyutu (karakter)")
    parser.add_argument("--overlap", type=int, default=80, help="Ardisik chunk'lar arasi ortusme (karakter)")
    args = parser.parse_args()

    source_path = Path(args.file)
    text = source_path.read_text(encoding="utf-8")

    chunks = chunk_text(text, size=args.size, overlap=args.overlap)

    print(f"Kaynak dosya : {source_path.name}")
    print(f"Toplam karakter: {len(text)}")
    print(f"Chunk boyutu   : {args.size} | Overlap: {args.overlap}")
    print(f"Uretilen chunk sayisi: {len(chunks)}")
    print("=" * 60)

    for chunk in chunks:
        preview = chunk.text.replace("\n", " ")
        print(f"\n[Chunk {chunk.index}] karakter {chunk.start}-{chunk.end} ({len(chunk.text)} kar.)")
        print(preview)

    print("\n" + "=" * 60)
    print("Ozet: her chunk'in basi ve sonu, bir onceki/sonraki chunk ile")
    print("ortusen kucuk bir bolge icerir. Bu sayede bir chunk tek basina")
    print("okundugunda bile cumle ortasinda kesilme riski azalir.")


if __name__ == "__main__":
    main()
