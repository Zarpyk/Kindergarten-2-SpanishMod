import csv
import time
import threading
from collections import defaultdict
from openai import OpenAI
from concurrent.futures import ThreadPoolExecutor, as_completed
import tiktoken

client = OpenAI(api_key="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")

SYSTEM_PROMPT = (
    "You are an English to Spanish translator tasked with translating death messages from a video game.\n\n"
    "- These are humorous death messages that appear when a player character dies in various ways.\n"
    "- Maintain the dark humor and sarcastic tone typical of the game.\n"
    "- Keep the messages concise and punchy, similar to the original.\n"
    "- The context is a school setting with 5-year-old children characters (except defined adults).\n"
    "- Preserve any character names mentioned (like Nugget, Kevin, Booboo, etc.).\n"
    "- Do not translate the word 'goo' as it's a game-specific term.\n"
    "- Maintain the casual, slightly mocking tone of the original messages.\n\n"
    "Translate the death messages in the specified format while keeping Field names:\n\n"
    "# Steps\n"
    "1. Identify the death scenario context from the Field name.\n"
    "2. Translate maintaining the sarcastic/humorous tone.\n"
    "3. Keep character names and game-specific terms like 'goo'.\n"
    "4. Ensure the message fits the death scenario described.\n\n"
    "# Output Format\n"
    "- [Field: FIELD_NAME]\nSpanish Translation\n\n"
    "# Notes\n"
    "- Remember these are meant to be darkly humorous death messages.\n"
    "- Keep the same length and impact as the original."
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
def translate_chunk(chunk, chunk_idx, total_chunks):
    global TOKENS_USED, WINDOW_START

    prompt = "\n\n".join(f"[Field: {field}]\n{text}" for field, text in chunk)
    estimated_tokens = estimate_tokens(SYSTEM_PROMPT + prompt)

    with LOCK:
        now = time.time()
        elapsed = now - WINDOW_START
        if elapsed > 60:
            TOKENS_USED = 0
            WINDOW_START = now
        elif TOKENS_USED + estimated_tokens > TOKENS_PER_MINUTE_LIMIT:
            wait_time = 60 - elapsed
            print(f"[Chunk {chunk_idx}] Esperando {wait_time:.2f} segundos para respetar el límite de tokens/min...")
            time.sleep(wait_time)
            TOKENS_USED = 0
            WINDOW_START = time.time()

        TOKENS_USED += estimated_tokens

    print(f"Traduciendo chunk {chunk_idx}/{total_chunks} con {len(chunk)} mensajes de muerte...")
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
    current_field = None
    for line in block.splitlines():
        if line.startswith("[Field:"):
            current_field = line.split(":")[1].strip(" ]")
            entries[current_field] = ""
        elif current_field is not None and line.strip():
            entries[current_field] += line + " "
    return {field: txt.strip().strip('"') for field, txt in entries.items()}

# Función principal para procesar CSV de Deaths
def process_deaths_csv(input_csv_path, output_csv_path, max_rows=100):
    # Leer el CSV original
    with open(input_csv_path, newline='', encoding='utf-8-sig') as infile:
        reader = csv.DictReader(infile)
        rows = list(reader)

    if max_rows == -1:
        rows_to_process = rows
        remaining = []
    else:
        rows_to_process = rows[:max_rows]
        remaining = rows[max_rows:]

    # Preparar pares (Field, Default) para traducir
    pairs = []
    for row in rows_to_process:
        field = row['Field']
        default_text = row['Default']
        if default_text and default_text.strip():
            pairs.append((field, default_text))

    if not pairs:
        print("No hay texto para traducir.")
        return

    # Dividir en chunks y traducir
    chunks = list(chunk_list(pairs, MAX_CHUNK_SIZE))
    total_chunks = len(chunks)
    
    print(f"Procesando {len(pairs)} mensajes de muerte en {total_chunks} chunks...")

    translated_blocks = []
    
    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as executor:
        futures = {}
        
        for idx, chunk in enumerate(chunks, 1):
            futures[executor.submit(translate_chunk, chunk, idx, total_chunks)] = idx

        for fut in as_completed(futures):
            chunk_idx = futures[fut]
            try:
                block = fut.result()
                translated_blocks.append((chunk_idx, block))
                print(f"Completado chunk {chunk_idx}/{total_chunks}")
            except Exception as e:
                print(f"Error en chunk {chunk_idx}: {e}")

    # Procesar traducciones
    blocks = [b for _, b in sorted(translated_blocks, key=lambda x: x[0])]
    trans_map = {}
    for blk in blocks:
        trans_map.update(parse_translated_block(blk))

    # Aplicar traducciones a las filas procesadas
    for row in rows_to_process:
        field = row['Field']
        if field in trans_map:
            row['Default [es]'] = trans_map[field]
        else:
            row['Default [es]'] = row['Default']  # Fallback al original

    # Para las filas no procesadas, copiar el original
    for row in remaining:
        row['Default [es]'] = row['Default']

    # Escribir el CSV de salida con la nueva columna
    fieldnames = ['Field', 'Default', 'Default [es]']
    
    with open(output_csv_path, 'w', newline='', encoding='utf-8-sig') as outfile:
        writer = csv.DictWriter(outfile, fieldnames=fieldnames, quoting=csv.QUOTE_MINIMAL)
        writer.writeheader()
        writer.writerows(rows_to_process)
        if max_rows != -1:
            writer.writerows(remaining)

    print(f"Traducción de mensajes de muerte completada. Guardado en: {output_csv_path}")
    print(f"Se tradujeron {len(trans_map)} mensajes de {len(pairs)} totales.")


input_dir = r'C:\Development\Projects\Kindergarten-2-SpanishMod\Kindergtarten 3\TranslationScripts\Deaths.csv'
output_dir = r'C:\Development\Projects\Kindergarten-2-SpanishMod\Kindergtarten 3\TranslationScripts\Deaths_es_new.csv'

process_deaths_csv(input_dir, output_dir, max_rows=-1)