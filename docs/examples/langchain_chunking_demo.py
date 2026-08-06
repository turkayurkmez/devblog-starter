"""
langchain_text_splitters ile "anlamli" chunking demosu.

chunking_demo.py'daki naif yaklasimin aksine burada iki asamali bolme
kullanilir:

  1) MarkdownHeaderTextSplitter — once dokumani basliklarina (#, ##) gore
     boler. Boylece hicbir chunk bir basligin ortasindan kesilmez ve her
     chunk hangi baslik altinda oldugunu metadata olarak tasir.
  2) RecursiveCharacterTextSplitter — her baslik bolumu hala cok
     buyukse, once paragraf (\n\n), sonra cumle (". "), sonra kelime
     sinirindan bolerek kucultur. Yani "once buyuk anlam biriminden kes,
     olmuyorsa kucul" mantigini izler.

Kullanim:
    py langchain_chunking_demo.py
    py langchain_chunking_demo.py --file ../10-rag-mimarisi.md --size 400 --overlap 80
"""

import argparse
from pathlib import Path

from langchain_text_splitters import (
    MarkdownHeaderTextSplitter,
    RecursiveCharacterTextSplitter,
)


def main() -> None:
    parser = argparse.ArgumentParser(description="langchain_text_splitters ile chunking demosu")
    parser.add_argument(
        "--file",
        default=str(Path(__file__).parent.parent / "10-rag-mimarisi.md"),
        help="Chunk'lanacak dosya (varsayilan: docs/10-rag-mimarisi.md)",
    )
    parser.add_argument("--size", type=int, default=400, help="Hedef chunk boyutu (karakter)")
    parser.add_argument("--overlap", type=int, default=80, help="Ardisik chunk'lar arasi ortusme (karakter)")
    args = parser.parse_args()

    source_path = Path(args.file)
    text = source_path.read_text(encoding="utf-8")

    # 1. asama: baslik sinirlarindan bol, her parcaya hangi baslik(lar)
    # altinda oldugunu metadata olarak isle.
    header_splitter = MarkdownHeaderTextSplitter(
        headers_to_split_on=[("#", "h1"), ("##", "h2")],
        strip_headers=False,
    )
    header_sections = header_splitter.split_text(text)

    # 2. asama: hala size'dan buyuk kalan bolumleri paragraf -> cumle ->
    # kelime hiyerarsisiyle kucult.
    recursive_splitter = RecursiveCharacterTextSplitter(
        chunk_size=args.size,
        chunk_overlap=args.overlap,
        separators=["\n\n", "\n", ". ", " ", ""],
    )
    final_chunks = recursive_splitter.split_documents(header_sections)

    print(f"Kaynak dosya : {source_path.name}")
    print(f"Toplam karakter: {len(text)}")
    print(f"Hedef chunk boyutu: {args.size} | Overlap: {args.overlap}")
    print(f"Baslik bolumu sayisi (1. asama): {len(header_sections)}")
    print(f"Nihai chunk sayisi (2. asama)  : {len(final_chunks)}")
    print("=" * 60)

    for i, chunk in enumerate(final_chunks):
        header_path = " > ".join(
            v for k, v in chunk.metadata.items() if k in ("h1", "h2")
        ) or "(baslik yok)"
        preview = chunk.page_content.replace("\n", " ").strip()
        print(f"\n[Chunk {i}] baslik: {header_path} ({len(chunk.page_content)} kar.)")
        print(preview)

    print("\n" + "=" * 60)
    print("Ozet: chunk'lar artik bir basligin ortasindan kesilmiyor ve her")
    print("chunk hangi baslik altinda oldugunu (h1/h2) metadata olarak")
    print("tasiyor. Bu metadata, retrieval sonrasi chunk'i kaynagina geri")
    print("baglamak veya kullaniciya alinti gostermek icin kullanilabilir.")


if __name__ == "__main__":
    main()
