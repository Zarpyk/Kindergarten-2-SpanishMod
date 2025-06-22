import csv
import time
import threading
from collections import defaultdict
from openai import OpenAI
from concurrent.futures import ThreadPoolExecutor, as_completed
import tiktoken

client = OpenAI(api_key="open-ai-api-key")

SYSTEM_PROMPT = (
    "You are an English to Spanish translator tasked with maintaining the humor and style of messages.\n\n"
    "- Preserve rhymes marked with <wave>. If there's no direct translation, place the correct translation in parentheses only if it changes the meaning significantly, but do not force the rhyme.\n"
    "- Retain the specific humor and occasional adult vocabulary used by characters, noting that all are 5-year-old children except a defined list of adults.\n"
    "- Do not translate the word 'goo'.\n"
    "- Maintain existing symbols and do not introduce extra phrases.\n\n"
    "Translate texts in the specified format while keeping Entry ID numbers:\n\n"
    "# Steps\n"
    "1. Identify text marked with <wave> for rhyme preservation.\n"
    "2. Translate humor and maintain style, noting character age and roles.\n"
    "3. Retain the word 'goo' and existing symbols.\n"
    "4. Format translations with Entry ID structure.\n\n"
    "# Output Format\n"
    "- [Entry ID: X]\nTranslation\n\n"
    "# Notes\n"
    "- Remember character distinctions and context while translating.\n"
    "- Avoid adding phrases or symbols beyond those provided."
)

MAX_CHUNK_SIZE = 50
MAX_WORKERS = 2

# Control de tokens
TOKENS_PER_MINUTE_LIMIT = 25000
TOKENS_USED = 0
WINDOW_START = time.time()
LOCK = threading.Lock()

# Estimador de tokens
def estimate_tokens(text, model="gpt-4"):
    encoding = tiktoken.encoding_for_model(model)
    return len(encoding.encode(text))

# Control de lista en chunks
def chunk_list(lst, chunk_size):
    for i in range(0, len(lst), chunk_size):
        yield lst[i:i + chunk_size]

# Traducción con control de tokens
def translate_chunk(chunk, conv_id, idx, total_chunks):
    global TOKENS_USED, WINDOW_START

    prompt = "\n\n".join(f"[Entry ID: {eid}]\n{text}" for eid, text in chunk)
    estimated_tokens = estimate_tokens(SYSTEM_PROMPT + prompt)

    with LOCK:
        now = time.time()
        elapsed = now - WINDOW_START
        if elapsed > 60:
            TOKENS_USED = 0
            WINDOW_START = now
        elif TOKENS_USED + estimated_tokens > TOKENS_PER_MINUTE_LIMIT:
            wait_time = 60 - elapsed
            print(f"[{conv_id} - Chunk {idx}] Esperando {wait_time:.2f} segundos para respetar el límite de 30k tokens/min...")
            time.sleep(wait_time)
            TOKENS_USED = 0
            WINDOW_START = time.time()

        TOKENS_USED += estimated_tokens

    print(f"[{conv_id}] Traduciendo chunk {idx}/{total_chunks} con {len(chunk)} textos...")
    response = client.chat.completions.create(
        model="gpt-4.1-2025-04-14",
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": prompt}
        ],
        temperature=0.5
    )

    return response.choices[0].message.content.strip()

# Procesar traducción en bloques
def parse_translated_block(block):
    entries = {}
    current_id = None
    for line in block.splitlines():
        if line.startswith("[Entry ID:"):
            current_id = int(line.split(":")[1].strip(" ]"))
            entries[current_id] = ""
        elif current_id is not None:
            entries[current_id] += line + " "
    return {eid: txt.strip().strip('"') for eid, txt in entries.items()}

# Función principal
def process_csv(input_csv_path, output_csv_path, max_rows=100):
    with open(input_csv_path, newline='', encoding='utf-8-sig') as infile:
        reader = list(csv.DictReader(infile))

    if max_rows == -1:
        rows_to_process = reader
        remaining = []
    else:
        rows_to_process = reader[:max_rows]
        remaining = reader[max_rows:]

    grouped = defaultdict(list)
    for row in rows_to_process:
        grouped[row['Conversation ID']].append(row)

    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as executor:
        futures = {}

        for conv_id, entries in grouped.items():
            pairs = []
            for row in entries:
                eid = int(row['Entry ID'])
                if row.get('Original Text'):
                    pairs.append((eid, row['Original Text']))
                if row.get('Original Menu'):
                    pairs.append((eid, row['Original Menu']))
            if not pairs:
                continue

            chunks = list(chunk_list(pairs, MAX_CHUNK_SIZE))
            total = len(chunks)
            for idx, chunk in enumerate(chunks, 1):
                futures[executor.submit(translate_chunk, chunk, conv_id, idx, total)] = (conv_id, idx)

        translated_blocks = defaultdict(list)
        for fut in as_completed(futures):
            conv_id, idx = futures[fut]
            try:
                block = fut.result()
                translated_blocks[conv_id].append((idx, block))
            except Exception as e:
                print(f"Error en conversación {conv_id}, chunk {idx}: {e}")

    for conv_id, entries in grouped.items():
        if conv_id in translated_blocks:
            blocks = [b for _, b in sorted(translated_blocks[conv_id], key=lambda x: x[0])]
            trans_map = {}
            for blk in blocks:
                trans_map.update(parse_translated_block(blk))

            for row in entries:
                eid = int(row['Entry ID'])
                if row.get('Original Text') and eid in trans_map:
                    row['Translated Text [es]'] = trans_map[eid]
                if row.get('Original Menu') and eid in trans_map:
                    row['Translated Menu [es]'] = trans_map[eid]

    for row in remaining:
        if row.get('Original Text'):
            row['Translated Text [es]'] = row['Original Text']
        if row.get('Original Menu'):
            row['Translated Menu [es]'] = row['Original Menu']

    with open(output_csv_path, 'w', newline='', encoding='utf-8-sig') as outfile:
        writer = csv.DictWriter(outfile, fieldnames=reader[0].keys(), quoting=csv.QUOTE_MINIMAL)
        writer.writeheader()
        for conv_id in grouped:
            writer.writerows(grouped[conv_id])
        if max_rows != -1:
            writer.writerows(remaining)

    print(f"Traducción completada. Guardado en: {output_csv_path}")

# Rutas de entrada/salida
input_dir = r'C:\Development\Projects\Kindergarten-2-SpanishMod\Kindergtarten 3\TranslationScripts\Dialogue_es.csv'
output_dir = r'C:\Development\Projects\Kindergarten-2-SpanishMod\Kindergtarten 3\TranslationScripts\Dialogue_es_new.csv'

process_csv(input_dir, output_dir, max_rows=-1)
