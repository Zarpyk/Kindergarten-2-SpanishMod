import csv
import time
import threading
from collections import defaultdict
from openai import OpenAI
from concurrent.futures import ThreadPoolExecutor, as_completed
import tiktoken

client = OpenAI(api_key="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")

SYSTEM_PROMPT = (
    "You are an English to Spanish translator for a video game quest system.\n\n"
    "- Translate quest names, descriptions, and objectives while maintaining clarity and game context.\n"
    "- Keep the tone appropriate for a children's game but preserve any humor or character personality.\n"
    "- Do not translate character names, locations, or technical terms like 'goo'.\n"
    "- Maintain game terminology consistency (e.g., 'classroom' = 'aula', 'recess' = 'recreo').\n"
    "- Keep quest objectives clear and actionable in Spanish.\n\n"
    "Translate texts in the specified format while keeping Quest Name as reference:\n\n"
    "# Steps\n"
    "1. Identify the type of content (quest name, description, objective).\n"
    "2. Translate maintaining game context and clarity.\n"
    "3. Preserve character names and technical terms.\n"
    "4. Format translations with Quest Name structure.\n\n"
    "# Output Format\n"
    "- [Quest: QUEST_NAME - Field: FIELD_NAME]\nTranslation\n\n"
    "# Notes\n"
    "- Maintain consistency across quest translations.\n"
    "- Keep objectives clear and concise in Spanish."
)

MAX_CHUNK_SIZE = 30
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
def translate_chunk(chunk, batch_id, idx, total_chunks):
    global TOKENS_USED, WINDOW_START

    prompt = "\n\n".join(f"[Quest: {quest_name} - Field: {field_name}]\n{text}" 
                        for quest_name, field_name, text in chunk)
    estimated_tokens = estimate_tokens(SYSTEM_PROMPT + prompt)

    with LOCK:
        now = time.time()
        elapsed = now - WINDOW_START
        if elapsed > 60:
            TOKENS_USED = 0
            WINDOW_START = now
        elif TOKENS_USED + estimated_tokens > TOKENS_PER_MINUTE_LIMIT:
            wait_time = 60 - elapsed
            print(f"[Batch {batch_id} - Chunk {idx}] Esperando {wait_time:.2f} segundos para respetar el límite de tokens/min...")
            time.sleep(wait_time)
            TOKENS_USED = 0
            WINDOW_START = time.time()

        TOKENS_USED += estimated_tokens

    print(f"[Batch {batch_id}] Traduciendo chunk {idx}/{total_chunks} con {len(chunk)} textos...")
    response = client.chat.completions.create(
        model="gpt-4.1-2025-04-14",
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": prompt}
        ],
        temperature=0.3
    )

    return response.choices[0].message.content.strip()

# Procesar traducción en bloques
def parse_translated_block(block):
    translations = {}
    current_key = None
    current_text = []
    
    for line in block.splitlines():
        if line.startswith("[Quest:"):
            if current_key and current_text:
                translations[current_key] = " ".join(current_text).strip().strip('"')
            # Extraer quest name y field name
            parts = line.split(" - Field: ")
            quest_name = parts[0].replace("[Quest: ", "")
            field_name = parts[1].replace("]", "")
            current_key = (quest_name, field_name)
            current_text = []
        elif current_key is not None and line.strip():
            current_text.append(line.strip())
    
    # Agregar el último elemento
    if current_key and current_text:
        translations[current_key] = " ".join(current_text).strip().strip('"')
    
    return translations

# Obtener campos a traducir de una fila
def get_translatable_fields(row):
    """Extrae campos que necesitan traducción de una fila del CSV"""
    fields_to_translate = []
    
    # Mapeo de campos originales a campos de traducción
    field_mapping = {
        'Display Name': 'Translated Display Name [es]',
        'Group': 'Translated Group [es]',
        'Description': 'Translated Description [es]',
        'Success Description': 'Translated Success Description [es]',
        'Failure Description': 'Translated Failure Description [es]'
    }
    
    # Agregar campos básicos
    for original_field, translated_field in field_mapping.items():
        if row.get(original_field) and row[original_field].strip():
            fields_to_translate.append((original_field, translated_field, row[original_field]))
    
    # Agregar entradas numeradas (Entry 1, Entry 2, etc.)
    for i in range(1, 8):  # Entries 1-7 según el CSV
        entry_field = f'Entry {i}'
        translated_field = f'Translated Entry [{i}]'
        if row.get(entry_field) and row[entry_field].strip():
            fields_to_translate.append((entry_field, translated_field, row[entry_field]))
    
    return fields_to_translate

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

    # Recopilar todos los textos que necesitan traducción
    translation_pairs = []
    row_field_mapping = {}  # Para mapear de vuelta las traducciones
    
    for row_idx, row in enumerate(rows_to_process):
        quest_name = row['Name']
        if not quest_name:  # Saltar filas vacías
            continue
            
        fields = get_translatable_fields(row)
        for original_field, translated_field, text in fields:
            key = (quest_name, original_field)
            translation_pairs.append((quest_name, original_field, text))
            if row_idx not in row_field_mapping:
                row_field_mapping[row_idx] = {}
            row_field_mapping[row_idx][key] = translated_field

    if not translation_pairs:
        print("No se encontraron textos para traducir.")
        return

    # Procesar traducciones en chunks
    chunks = list(chunk_list(translation_pairs, MAX_CHUNK_SIZE))
    total_chunks = len(chunks)
    
    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as executor:
        futures = {}
        
        for idx, chunk in enumerate(chunks, 1):
            future = executor.submit(translate_chunk, chunk, "QUESTS", idx, total_chunks)
            futures[future] = idx

        # Recopilar resultados
        all_translations = {}
        for future in as_completed(futures):
            chunk_idx = futures[future]
            try:
                block = future.result()
                translations = parse_translated_block(block)
                all_translations.update(translations)
                print(f"Completado chunk {chunk_idx}/{total_chunks}")
            except Exception as e:
                print(f"Error en chunk {chunk_idx}: {e}")

    # Aplicar traducciones a las filas
    for row_idx, row in enumerate(rows_to_process):
        if row_idx in row_field_mapping:
            for (quest_name, original_field), translated_field in row_field_mapping[row_idx].items():
                key = (quest_name, original_field)
                if key in all_translations:
                    row[translated_field] = all_translations[key]
                    print(f"Traducido: {quest_name} - {original_field}")

    # Mantener filas no procesadas sin cambios
    for row in remaining:
        for field in row.keys():
            if field.startswith('Translated ') and field.endswith(' [es]'):
                original_field = field.replace('Translated ', '').replace(' [es]', '')
                if original_field in row and row[original_field]:
                    row[field] = row[original_field]  # Copiar original si no hay traducción

    # Escribir resultado
    all_rows = rows_to_process + remaining
    with open(output_csv_path, 'w', newline='', encoding='utf-8-sig') as outfile:
        if all_rows:
            writer = csv.DictWriter(outfile, fieldnames=all_rows[0].keys(), quoting=csv.QUOTE_MINIMAL)
            writer.writeheader()
            writer.writerows(all_rows)

    print(f"Traducción completada. Guardado en: {output_csv_path}")
    print(f"Procesadas {len(translation_pairs)} traducciones en {len(rows_to_process)} quests.")

# Rutas de entrada/salida
input_file = r'C:\Development\Projects\Kindergarten-2-SpanishMod\Kindergtarten 3\TranslationScripts\Quests_es.csv'
output_file = r'C:\Development\Projects\Kindergarten-2-SpanishMod\Kindergtarten 3\TranslationScripts\Quests_es_new.csv'

process_csv(input_file, output_file, max_rows=-1)